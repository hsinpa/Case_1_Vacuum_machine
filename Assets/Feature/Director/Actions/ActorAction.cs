namespace Hsinpa.Director
{
    /// <summary>
    /// One thing a CutsceneActor does, such as walking somewhere. Actions run one after another in the
    /// actor's queue; each one handles its own failures and finishes, so a broken action never stalls
    /// the queue.
    /// <para>Lifecycle: CanStart (polled until true) → Begin → Tick every frame → Finish() from inside.
    /// If the actor is disabled mid-action, Interrupt is called and Begin runs again on re-enable.
    /// If the action is dropped (ClearActions, actor destroyed), Cancel is called instead.</para>
    /// <para>An action runs once: IsDone never resets, so make a new instance for every Enqueue.</para>
    /// </summary>
    public abstract class ActorAction
    {
        public bool IsDone { get; private set; }

        /// <summary>Call when the action has finished, successfully or not.</summary>
        protected void Finish() => IsDone = true;

        /// <summary>Polled each frame before Begin. Return false to wait, e.g. until a component has started.</summary>
        public virtual bool CanStart(CutsceneActor actor) => true;

        /// <summary>Starts the action. May call Finish() right away for an instant action or a failure.</summary>
        public abstract void Begin(CutsceneActor actor);

        /// <summary>Called every frame while the action runs.</summary>
        public virtual void Tick(CutsceneActor actor) { }

        /// <summary>The actor was disabled mid-action; Begin is called again when it is re-enabled. Release events here.</summary>
        public virtual void Interrupt(CutsceneActor actor) { }

        /// <summary>The action is being dropped for good. Release events and stop whatever it started.</summary>
        public virtual void Cancel(CutsceneActor actor) { }
    }
}
