using System.Collections;
using Hsinpa.Character;
using Hsinpa.Map;
using UnityEngine;

namespace Hsinpa.Director
{
    /// <summary>
    /// Script commands for the scene's CameraFollow.
    /// <code>
    /// # func camera_follow("koren")        follow an actor, with the usual dead zone        (does not wait)
    /// # func camera_pan(12, 4, 1.5f)       glide to centre on a cell over 1.5 s, then hold  (waits)
    /// # func camera_pan(12, 4, 0)          cut straight to a cell                            (waits)
    /// # func camera_reset()                follow the Inspector target (the player) again   (does not wait)
    /// </code>
    /// When a story ends the camera follows the Inspector target again by itself.
    /// </summary>
    static class CameraCommands
    {
        public static void Register(CommandRegistry commands, DialogueRunner runner)
        {
            commands.Register<CutsceneActor>("camera_follow", actor => RequireCamera().Follow(actor.transform));

            commands.RegisterWaiting<int, int, float>("camera_pan", (x, y, seconds) =>
            {
                CameraFollow camera = RequireCamera();
                GridMap2D map = GridMap2D.Active;
                if (map == null) throw new CommandError("no GridMap2D in the scene to turn cells into positions");

                camera.PanTo(map.CellCenterToWorld(new Vector2Int(x, y)), seconds);
                return WaitForPan(camera);
            });

            commands.Register("camera_reset", () => RequireCamera().ResetTarget());

            runner.Finished += () =>
            {
                if (CameraFollow.Active != null) CameraFollow.Active.ResetTarget();
            };
        }

        static IEnumerator WaitForPan(CameraFollow camera)
        {
            while (camera != null && camera.IsPanning) yield return null;
        }

        static CameraFollow RequireCamera() =>
            CameraFollow.Active != null ? CameraFollow.Active : throw new CommandError("no enabled CameraFollow in the scene");
    }
}
