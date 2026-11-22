using System;
using System.Collections.Generic;
using Hsinpa.Map;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Hsinpa.Character
{
    public enum AgentState
    {
        /// <summary>No order. A keyboard step may still be in progress.</summary>
        Idle,
        /// <summary>Following a path.</summary>
        Walking,
        /// <summary>Following a path, but the next cell is held by someone; retrying shortly.</summary>
        Waiting,
    }

    /// <summary>
    /// Moves a character around a GridMap2D. Whoever controls the character (player input, NPC AI)
    /// gives orders here: MoveTo for a destination, TryStep for a single step.
    /// Owns the GridMover, so the character's cells are claimed while enabled and released when disabled.
    /// </summary>
    public class GridAgent : MonoBehaviour
    {
        static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
        };

        [SerializeField, Tooltip("Optional. Leave empty to use GridMap2D.Active.")]
        private GridMap2D gridMap;

        [SerializeField, Range(0.1f, 10f), Tooltip("Cells per second.")]
        private float moveSpeed = 1f;

        [Header("Debug")]
        [SerializeField]
        private bool drawPath = true;

        GridMover _mover;

        // The path being followed, and two scratch lists for planning. Swapped rather than copied.
        List<Vector2Int> _path = new List<Vector2Int>();
        List<Vector2Int> _candidate = new List<Vector2Int>();
        List<Vector2Int> _best = new List<Vector2Int>();
        int _pathIndex;

        Vector2Int _orderTarget;
        bool _faceTargetOnArrival;
        float _waitTimer;
        int _blockedRetries;

        public Vector2Int CurrentCell => _mover?.CurrentCell ?? default;

        /// <summary>False until Start has run (e.g. while a cutscene keeps the character inactive). Orders are ignored until then.</summary>
        public bool IsReady => _mover != null;
        public Vector2Int Facing { get; private set; } = Vector2Int.down;
        public AgentState State { get; private set; } = AgentState.Idle;

        /// <summary>
        /// True while a step is in progress, and also between steps while a path remains,
        /// so a walk animation does not flicker to idle at every cell.
        /// </summary>
        public bool IsMoving => _mover != null && (_mover.IsMoving || State == AgentState.Walking);

        public GridMap2D Map => gridMap;

        public event Action Arrived;
        public event Action<PathResult> PathFailed;

        #region Unity lifecycle

        void Start()
        {
            if (gridMap == null) gridMap = GridMap2D.Active;
            if (gridMap == null)
            {
                Debug.LogError($"{name}: no GridMap2D assigned and none active in the scene. The agent will not move.", this);
                return;
            }

            // Created in Start rather than Awake so the collision tilemap is ready when the graph bakes.
            _mover = new GridMover(transform, gridMap.Layout, gridMap.Graph, this);
        }

        void OnEnable()
        {
            // The first enable runs before Start; the mover claims its cell when it is created.
            _mover?.Reoccupy();
        }

        void OnDisable()
        {
            ClearPath();
            _mover?.ReleaseAll();
        }

        void Update()
        {
            if (_mover == null) return;

            _mover.Tick(moveSpeed * Time.deltaTime);

            // A step always finishes before the next decision is made.
            if (_mover.IsMoving || State == AgentState.Idle) return;

            if (State == AgentState.Waiting)
            {
                _waitTimer -= Time.deltaTime;
                if (_waitTimer > 0f) return;
            }

            AdvancePath();
        }

        #endregion

        #region Orders

        /// <summary>
        /// Walks to the target cell. If the target cannot be entered (a wall, a prop, an NPC), walks to
        /// the nearest free cell beside it and turns to face it. Callable mid-step: the new path starts
        /// from the cell being stepped into. Returns false, leaving any current path untouched, when
        /// there is nowhere to go.
        /// </summary>
        public bool MoveTo(Vector2Int target)
        {
            if (_mover == null) return false;

            if (!TryPlan(target, allowThroughOccupants: true, out bool faceTarget, out PathResult failure))
            {
                PathFailed?.Invoke(failure);
                return false;
            }

            _orderTarget = target;
            _faceTargetOnArrival = faceTarget;
            _blockedRetries = 0;
            State = AgentState.Walking;
            return true;
        }

        /// <summary>
        /// Takes one step in a direction (straight or diagonal), cancelling any path. A blocked diagonal
        /// slides along its horizontal part, else its vertical part, so holding a diagonal against a wall
        /// still moves. Faces the way it actually moves, or the requested direction when fully blocked.
        /// Returns false while a step is still in progress or when blocked.
        /// </summary>
        public bool TryStep(Vector2Int direction)
        {
            if (_mover == null || direction == Vector2Int.zero) return false;

            ClearPath();
            if (_mover.IsMoving) return false;

            if (StartStep(direction)) return true;

            bool isDiagonal = direction.x != 0 && direction.y != 0;
            if (isDiagonal && (StartStep(new Vector2Int(direction.x, 0)) || StartStep(new Vector2Int(0, direction.y))))
                return true;

            Facing = direction;
            return false;
        }

        bool StartStep(Vector2Int direction)
        {
            if (!_mover.TryStartMove(direction)) return false;
            Facing = direction;
            return true;
        }

        /// <summary>Drops the current path. A step in progress still finishes.</summary>
        public void Stop() => ClearPath();

        /// <summary>Turns without moving. Ignored for a zero direction.</summary>
        public void Face(Vector2Int direction)
        {
            if (direction != Vector2Int.zero) Facing = direction;
        }

        /// <summary>
        /// Places the character on a cell at once, cancelling any path or step, and keeps the grid's
        /// occupancy in sync (never set transform.position directly). Works before Start too, e.g. on a
        /// character a cutscene has not activated yet: the mover claims the cell when it is created.
        /// A non-zero facing turns the character. Returns false when the cell is blocked or taken.
        /// </summary>
        public bool Warp(Vector2Int cell, Vector2Int facing = default)
        {
            if (_mover == null)
            {
                GridMap2D map = gridMap != null ? gridMap : GridMap2D.Active;
                if (map == null || !map.Graph.IsFree(cell, this)) return false;
                transform.position = GridMover.CellToPivot(map.Layout, cell);
            }
            else
            {
                if (!_mover.Warp(cell, claim: isActiveAndEnabled)) return false;
                ClearPath();
            }

            Face(facing);
            return true;
        }

        #endregion

        #region Path following

        void AdvancePath()
        {
            if (_pathIndex >= _path.Count)
            {
                Arrive();
                return;
            }

            Vector2Int direction = _path[_pathIndex] - _mover.CurrentCell;
            Facing = direction;

            if (_mover.TryStartMove(direction))
            {
                _pathIndex++;
                _blockedRetries = 0;
                State = AgentState.Walking;
                return;
            }

            OnBlocked();
        }

        // First block: wait and retry the same step, since the blocker is often just passing.
        // Later blocks: try a route around the blocker's current position, else keep waiting.
        void OnBlocked()
        {
            if (_blockedRetries >= GridAgentSettings.MaxBlockedRetries)
            {
                ClearPath();
                PathFailed?.Invoke(PathResult.Unreachable);
                return;
            }

            _blockedRetries++;

            if (_blockedRetries > 1 && TryPlan(_orderTarget, allowThroughOccupants: false, out bool faceTarget, out _))
            {
                _faceTargetOnArrival = faceTarget;
                State = AgentState.Walking;
                return;
            }

            // Jitter keeps two agents blocking each other from retrying in lockstep forever.
            const float wait = GridAgentSettings.BlockedWaitTime;
            _waitTimer = wait + Random.Range(0f, wait * GridAgentSettings.BlockedWaitJitter);
            State = AgentState.Waiting;
        }

        void Arrive()
        {
            if (_faceTargetOnArrival)
            {
                Vector2Int toTarget = _orderTarget - _mover.CurrentCell;
                if (Mathf.Abs(toTarget.x) + Mathf.Abs(toTarget.y) == 1) Facing = toTarget;
            }

            ClearPath();
            Arrived?.Invoke();
        }

        void ClearPath()
        {
            _path.Clear();
            _pathIndex = 0;
            _faceTargetOnArrival = false;
            State = AgentState.Idle;
        }

        #endregion

        #region Planning

        /// <summary>
        /// Plans a path to the target, or beside it when the target cannot be entered, and makes it the
        /// current path. Avoids occupants first; if allowed, falls back to planning through them, so the
        /// agent walks up to the blocker and waits instead of refusing the order.
        /// </summary>
        bool TryPlan(Vector2Int target, bool allowThroughOccupants, out bool faceTarget, out PathResult failure)
        {
            GridGraph graph = gridMap.Graph;
            faceTarget = false;
            failure = PathResult.InvalidEndpoint;

            if (!graph.InBounds(target)) return false;

            // Planned from where the agent will stand once any step in progress finishes.
            Vector2Int origin = _mover.IsMoving ? _mover.TargetCell : _mover.CurrentCell;
            bool targetIsFree = graph.IsFree(target, this);

            if (TryPlanPass(origin, target, targetIsFree, ignoreOccupants: false, ref failure)
                || (allowThroughOccupants && TryPlanPass(origin, target, targetIsFree, ignoreOccupants: true, ref failure)))
            {
                faceTarget = !targetIsFree;
                (_path, _best) = (_best, _path);
                _pathIndex = 0;
                return true;
            }

            return false;
        }

        // Leaves the shortest path found in _best. A free target is the only goal; otherwise every cell
        // beside it is a goal and the shortest walk wins (the origin itself counts, at length 0).
        bool TryPlanPass(Vector2Int origin, Vector2Int target, bool targetIsFree, bool ignoreOccupants, ref PathResult failure)
        {
            GridPathfinder pathfinder = gridMap.Pathfinder;
            pathfinder.MaxExpansions = GridAgentSettings.MaxSearchCells;

            if (targetIsFree)
                return Search(pathfinder, origin, target, ignoreOccupants, ref failure);

            bool found = false;
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int goal = target + direction;
                PathResult result = pathfinder.FindPath(origin, goal, _candidate, this, ignoreOccupants);

                if (result != PathResult.Found)
                {
                    failure = MoreInformative(failure, result);
                    continue;
                }

                if (!found || _candidate.Count < _best.Count)
                {
                    (_best, _candidate) = (_candidate, _best);
                    found = true;
                }
            }
            return found;
        }

        bool Search(GridPathfinder pathfinder, Vector2Int origin, Vector2Int goal, bool ignoreOccupants, ref PathResult failure)
        {
            PathResult result = pathfinder.FindPath(origin, goal, _best, this, ignoreOccupants);
            if (result == PathResult.Found) return true;

            failure = MoreInformative(failure, result);
            return false;
        }

        // When several goals fail, report the reason closest to "a route might exist".
        static PathResult MoreInformative(PathResult a, PathResult b)
        {
            return Rank(b) > Rank(a) ? b : a;

            static int Rank(PathResult r) => r switch
            {
                PathResult.LimitReached => 2,
                PathResult.Unreachable => 1,
                _ => 0,
            };
        }

        #endregion

        void OnDrawGizmosSelected()
        {
            if (!drawPath || gridMap == null || _mover == null || _pathIndex >= _path.Count) return;

            Gizmos.color = State == AgentState.Waiting ? Color.yellow : Color.cyan;

            Vector3 previous = gridMap.CellCenterToWorld(_mover.IsMoving ? _mover.TargetCell : _mover.CurrentCell);
            for (int i = _pathIndex; i < _path.Count; i++)
            {
                Vector3 point = gridMap.CellCenterToWorld(_path[i]);
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
            Gizmos.DrawWireSphere(previous, 0.2f);
        }
    }
}
