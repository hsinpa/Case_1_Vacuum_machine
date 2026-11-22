using Hsinpa.Character;
using Hsinpa.Map;
using UnityEngine;

namespace Hsinpa.Director
{
    /// <summary>
    /// Walks the actor's GridAgent to a cell, or by an offset from where the actor stands when the walk
    /// starts (so queued offsets add up). Uses the agent's own speed, pathfinding and walk cycle.
    /// A path that fails is reported and skipped.
    /// </summary>
    public sealed class WalkAction : ActorAction
    {
        readonly Vector2Int _value;
        readonly bool _isOffset;

        CutsceneActor _actor;
        GridAgent _agent;
        bool _hasTarget;
        Vector2Int _target;
        bool _listening;

        WalkAction(Vector2Int value, bool isOffset)
        {
            _value = value;
            _isOffset = isOffset;
        }

        /// <summary>Walk to a grid cell.</summary>
        public static WalkAction To(Vector2Int cell) => new WalkAction(cell, isOffset: false);

        /// <summary>Walk by an offset; (0, 1) is one cell up.</summary>
        public static WalkAction By(Vector2Int offset) => new WalkAction(offset, isOffset: true);

        public override bool CanStart(CutsceneActor actor)
        {
            // No agent: start anyway, so Begin can report it and finish.
            if (_agent == null && !actor.TryGetComponent(out _agent)) return true;
            return _agent.IsReady;
        }

        public override void Begin(CutsceneActor actor)
        {
            _actor = actor;
            if (_agent == null)
            {
                Debug.LogWarning($"{actor.DisplayId}: cannot walk, it has no GridAgent.", actor);
                Finish();
                return;
            }

            // Resolved once, so a walk restarted after an interruption keeps its original destination.
            if (!_hasTarget)
            {
                _target = _isOffset ? _agent.CurrentCell + _value : _value;
                _hasTarget = true;
            }

            Listen(true);

            // On failure MoveTo raises PathFailed before returning, which finishes the action.
            if (!_agent.MoveTo(_target) && !IsDone)
            {
                Listen(false);
                Finish();
            }
        }

        public override void Interrupt(CutsceneActor actor)
        {
            // The agent drops its path itself when disabled; Begin walks on from wherever it stopped.
            Listen(false);
        }

        public override void Cancel(CutsceneActor actor)
        {
            if (_listening) _agent.Stop();
            Listen(false);
        }

        void OnArrived()
        {
            Listen(false);
            Finish();
        }

        void OnPathFailed(PathResult result)
        {
            Debug.LogWarning($"{_actor.DisplayId}: cannot walk to {_target} ({result}); skipping.", _actor);
            Listen(false);
            Finish();
        }

        void Listen(bool on)
        {
            if (on == _listening || _agent == null) return;
            _listening = on;

            if (on)
            {
                _agent.Arrived += OnArrived;
                _agent.PathFailed += OnPathFailed;
            }
            else
            {
                _agent.Arrived -= OnArrived;
                _agent.PathFailed -= OnPathFailed;
            }
        }
    }
}
