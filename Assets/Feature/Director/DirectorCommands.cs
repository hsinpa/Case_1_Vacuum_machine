namespace Hsinpa.Director
{
    /// <summary>
    /// Every group of script commands, in one place. To add commands:
    /// <list type="number">
    /// <item>Make a static class next to CoreCommands (e.g. ActorCommands), with a doc comment listing its
    /// commands as a writer would type them, and a Register method.</item>
    /// <item>Add one line below.</item>
    /// </list>
    /// In a handler, take a CutsceneActor parameter rather than an id string (the actor is looked up and a
    /// missing one reported for you), and throw CommandError when the scene cannot do what was asked.
    /// Use Register for commands that start something, RegisterWaiting for commands the story holds for.
    /// </summary>
    static class DirectorCommands
    {
        public static void RegisterAll(CommandRegistry commands, DialogueRunner runner)
        {
            CoreCommands.Register(commands);
            CameraCommands.Register(commands, runner);
        }
    }
}
