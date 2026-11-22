using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Hsinpa.Director
{
    /// <summary>
    /// Something dialogue scripts can direct, found by its id (e.g. "koren"); also the speaker name.
    /// Runs its actions one after another: walk then face means face after arriving. Different actors
    /// run at the same time. The actor itself knows nothing about walking or any other ability;
    /// each ActorAction finds the components it needs.
    /// </summary>
    [DisallowMultipleComponent]
    public class CutsceneActor : MonoBehaviour
    {
        [SerializeField, Tooltip("Name used by dialogue scripts and as the speaker name, e.g. koren.")]
        string id;

        [SerializeField, Tooltip("Called when a story starts, e.g. turn off player input or an NPC's wandering.")]
        UnityEvent onStoryStarted = new UnityEvent();

        [SerializeField, Tooltip("Called when the story ends; undo what On Story Started did.")]
        UnityEvent onStoryEnded = new UnityEvent();

        readonly Queue<ActorAction> _queue = new Queue<ActorAction>();
        ActorAction _current;
        bool _begun;

        public string Id => id;

        /// <summary>True while an action runs or waits in the queue.</summary>
        public bool IsBusy => _current != null || _queue.Count > 0;

        internal string DisplayId => string.IsNullOrEmpty(id) ? name : id;

        /// <summary>Adds an action after the ones already queued. Queued actions wait while the actor is inactive.</summary>
        public void Enqueue(ActorAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (action.IsDone)
                throw new ArgumentException($"{DisplayId}: this {action.GetType().Name} has already run; make a new one for every Enqueue.", nameof(action));
            if (action == _current || _queue.Contains(action))
                throw new ArgumentException($"{DisplayId}: this {action.GetType().Name} is already queued; make a new one for every Enqueue.", nameof(action));
            _queue.Enqueue(action);
        }

        // Called by DialogueRunner on every actor, inactive ones included, so an actor's own
        // components decide what a story means for them (the runner knows nothing about input or AI).
        internal void NotifyStoryStarted() => onStoryStarted.Invoke();
        internal void NotifyStoryEnded() => onStoryEnded.Invoke();

        /// <summary>Cancels the running action and drops the queue, e.g. when a cutscene is skipped.</summary>
        public void ClearActions()
        {
            if (_current != null && _begun) _current.Cancel(this);
            _current = null;
            _begun = false;
            _queue.Clear();
        }

        void Update()
        {
            // Loops so instant actions (face, show) chain within one frame instead of one per frame.
            // Begin and Tick can disable the actor (a hide action) or clear its queue (a listener that
            // stops the story), so the loop checks again after each and starts over from the new state.
            while (isActiveAndEnabled)
            {
                if (_current == null)
                {
                    if (_queue.Count == 0) return;
                    _current = _queue.Dequeue();
                    _begun = false;
                }

                ActorAction action = _current;

                if (!_begun)
                {
                    if (!action.CanStart(this)) return;
                    _begun = true;
                    action.Begin(this);
                    if (!IsStillRunning(action)) continue;
                }

                if (!action.IsDone)
                {
                    action.Tick(this);
                    if (!IsStillRunning(action)) continue;
                }
                if (!action.IsDone) return;

                _current = null;
            }
        }

        bool IsStillRunning(ActorAction action) => _current == action && isActiveAndEnabled;

        void OnDisable()
        {
            // Update stops while disabled; the running action starts again on re-enable.
            if (_current != null && _begun && !_current.IsDone)
            {
                _current.Interrupt(this);
                _begun = false;
            }
        }

        void OnDestroy() => ClearActions();
    }
}
