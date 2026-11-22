using System.Collections;
using UnityEngine;

namespace Hsinpa.Director
{
    /// <summary>
    /// The built-in script commands.
    /// <code>
    /// # func walk_to("koren", 12, 4)   walk to a grid cell                              (does not wait)
    /// # func walk("koren", 0, 1)       walk by an offset; (0, 1) is one cell up          (does not wait)
    /// # func wait(0.5f)                hold the story for some seconds                   (waits)
    /// # func wait_for("koren")         hold the story until koren has finished all its  (waits)
    ///                                  queued actions, e.g. arrived from its walks
    /// </code>
    /// </summary>
    static class CoreCommands
    {
        public static void Register(CommandRegistry commands)
        {
            commands.Register<CutsceneActor, int, int>("walk_to", (actor, x, y) =>
                actor.Enqueue(WalkAction.To(new Vector2Int(x, y))));

            commands.Register<CutsceneActor, int, int>("walk", (actor, dx, dy) =>
                actor.Enqueue(WalkAction.By(new Vector2Int(dx, dy))));

            commands.RegisterWaiting<float>("wait", Wait);
            commands.RegisterWaiting<CutsceneActor>("wait_for", WaitFor);
        }

        static IEnumerator Wait(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
        }

        // An inactive actor does not run its queue, so waiting on it would never end: stop waiting instead.
        static IEnumerator WaitFor(CutsceneActor actor)
        {
            while (actor != null && actor.isActiveAndEnabled && actor.IsBusy) yield return null;
        }
    }
}
