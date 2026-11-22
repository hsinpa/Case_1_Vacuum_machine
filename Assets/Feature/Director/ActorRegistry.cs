using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hsinpa.Director
{
    /// <summary>
    /// The CutsceneActors of the loaded scenes, by id. The runner refreshes it when an event starts, so the
    /// cost of searching every actor does not matter. Inactive actors (e.g. hidden until a cutscene shows
    /// them) are included; actors spawned while an event plays are found from the next event on.
    /// </summary>
    public sealed class ActorRegistry
    {
        readonly Dictionary<string, CutsceneActor> _byId = new Dictionary<string, CutsceneActor>(StringComparer.Ordinal);
        readonly List<CutsceneActor> _all = new List<CutsceneActor>();

        /// <summary>Every actor found at the last refresh, including ones without a usable id.</summary>
        public IReadOnlyList<CutsceneActor> All => _all;

        /// <summary>Searches the loaded scenes again, warning about missing and duplicate ids.</summary>
        public void Refresh()
        {
            _byId.Clear();
            _all.Clear();

            foreach (CutsceneActor actor in Object.FindObjectsByType<CutsceneActor>(FindObjectsInactive.Include))
            {
                _all.Add(actor);

                if (string.IsNullOrWhiteSpace(actor.Id))
                {
                    Debug.LogWarning($"{actor.name}: CutsceneActor has no id; scripts cannot use it.", actor);
                    continue;
                }
                if (_byId.TryGetValue(actor.Id, out CutsceneActor existing))
                {
                    Debug.LogWarning($"{actor.name}: id '{actor.Id}' is already used by {existing.name}; ignoring this one.", actor);
                    continue;
                }
                _byId.Add(actor.Id, actor);
            }
        }

        /// <summary>The actor with this id, if it was found at the last refresh and has not been destroyed since.</summary>
        public bool TryGet(string id, out CutsceneActor actor)
        {
            if (id != null && _byId.TryGetValue(id, out actor) && actor != null) return true;
            actor = null;
            return false;
        }
    }
}
