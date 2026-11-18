using Hsinpa.Map;
using UnityEngine;

namespace Hsinpa.Character
{
    /// <summary>
    /// Moves a transform one grid cell at a time. Collision is decided once, when a step starts;
    /// Tick only interpolates toward a target that is already known to be walkable.
    /// While stepping, both the cell being left and the cell being entered are held,
    /// so no other occupant can move into either.
    /// </summary>
    class GridMover
    {
        // The character pivot sits at the bottom-center of the cell it occupies.
        static readonly Vector3 PivotOffset = new Vector3(0.5f, 0f, 0f);
        static readonly Vector3 CellCenterOffset = new Vector3(0.5f, 0.5f, 0f);

        readonly Transform _transform;
        readonly GridLayout _grid;
        readonly GridGraph _graph;
        readonly object _owner;

        Vector3 _targetWorldPosition;

        public Vector2Int CurrentCell { get; private set; }
        public Vector2Int TargetCell { get; private set; }
        public bool IsMoving { get; private set; }

        public GridMover(Transform transform, GridLayout grid, GridGraph graph, object owner)
        {
            _transform = transform;
            _grid = grid;
            _graph = graph;
            _owner = owner;

            // Snap to the cell the character was placed in.
            Vector3 occupiedCellCenter = _transform.position - PivotOffset + CellCenterOffset;
            Vector3Int cell = _grid.WorldToCell(occupiedCellCenter);

            CurrentCell = new Vector2Int(cell.x, cell.y);
            TargetCell = CurrentCell;
            _transform.position = CellToWorld(CurrentCell);

            if (!_graph.TryOccupy(CurrentCell, _owner))
                Debug.LogWarning($"{transform.name} starts on cell {CurrentCell}, which is blocked or already occupied.", transform);
        }

        /// <summary>
        /// Starts a one-cell step, straight or diagonal. Returns false, and does nothing, when already
        /// moving or the step is blocked by terrain or another occupant (see GridGraph.CanStep).
        /// A diagonal step covers more distance, so at the same speed it takes about 1.41x as long.
        /// </summary>
        public bool TryStartMove(Vector2Int direction)
        {
            if (IsMoving || direction == Vector2Int.zero) return false;

            Vector2Int nextCell = CurrentCell + direction;
            if (!_graph.CanStep(CurrentCell, direction, _owner) || !_graph.TryOccupy(nextCell, _owner)) return false;

            TargetCell = nextCell;
            _targetWorldPosition = CellToWorld(nextCell);
            IsMoving = true;
            return true;
        }

        /// <summary>
        /// Advances the current step by the given distance (speed * deltaTime).
        /// </summary>
        public void Tick(float stepDistance)
        {
            if (!IsMoving) return;

            _transform.position = Vector3.MoveTowards(_transform.position, _targetWorldPosition, stepDistance);

            if (_transform.position == _targetWorldPosition)
            {
                _graph.Release(CurrentCell, _owner);
                CurrentCell = TargetCell;
                IsMoving = false;
            }
        }

        /// <summary>
        /// Frees every cell this mover holds. Call when the owner is destroyed or disabled.
        /// A step in progress is cancelled and the transform snaps back to the cell it started from.
        /// </summary>
        public void ReleaseAll()
        {
            _graph.Release(CurrentCell, _owner);
            _graph.Release(TargetCell, _owner);

            if (IsMoving)
            {
                IsMoving = false;
                TargetCell = CurrentCell;
                _transform.position = CellToWorld(CurrentCell);
            }
        }

        /// <summary>Claims the current cell again after ReleaseAll, e.g. when the owner is re-enabled.</summary>
        public bool Reoccupy()
        {
            return _graph.TryOccupy(CurrentCell, _owner);
        }

        Vector3 CellToWorld(Vector2Int cell)
        {
            Vector3 world = _grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0)) + PivotOffset;
            world.z = 0;
            return world;
        }
    }
}
