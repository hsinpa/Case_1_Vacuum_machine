using System.Collections.Generic;
using Ink.Runtime;
using Ink.UnityIntegration;
using UnityEngine;

namespace Hsinpa.Dialogue
{
    /// <summary>
    /// The ink stories for one scene, looked up by file name (without extension, e.g. "scene1_exterior").
    /// Each .ink file is imported by the ink integration as an InkFile that already holds the compiled
    /// story; drag the .ink files here.
    /// </summary>
    [CreateAssetMenu(fileName = "SceneDialogue", menuName = "Hsinpa/Dialogue/Scene Dialogue")]
    public class SceneDialogueAsset : ScriptableObject
    {
        [Tooltip("The scene's .ink files (imported as InkFile assets).")]
        [SerializeField] List<InkFile> _inkFiles = new List<InkFile>();

        [Tooltip("The story played by DialogueRunner.Play(). Empty means the first in the list.")]
        [SerializeField] InkFile _openingStory;

        [Tooltip("Play the opening story when the scene starts.")]
        [SerializeField] bool _playOnStart = true;

        public IReadOnlyList<InkFile> InkFiles => _inkFiles;
        public bool PlayOnStart => _playOnStart;

        /// <summary>The chosen opening story, or the first in the list when none is chosen. Null when the list is empty.</summary>
        public InkFile OpeningStory
        {
            get
            {
                if (_openingStory != null && _inkFiles.Contains(_openingStory)) return _openingStory;
                foreach (InkFile file in _inkFiles)
                    if (file != null) return file;
                return null;
            }
        }

        public bool HasStory(string fileName) => Find(fileName) != null;

        /// <summary>Builds a fresh Story at its start from one of this asset's files.</summary>
        public Story CreateStory(InkFile file)
        {
            if (file == null || !_inkFiles.Contains(file))
            {
                Debug.LogError($"{name}: '{(file != null ? file.name : "null")}' is not one of this asset's ink files.", this);
                return null;
            }
            return Compile(file);
        }

        /// <summary>Builds a fresh Story at its start from the file with this name. Each call returns an independent instance.</summary>
        public Story CreateStory(string fileName)
        {
            InkFile file = Find(fileName);
            if (file == null)
            {
                Debug.LogError($"{name}: no ink file named '{fileName}'.", this);
                return null;
            }
            return Compile(file);
        }

        Story Compile(InkFile file)
        {
            if (!file.isCompiled)
            {
                string reason = file.hasErrors ? "it has compile errors (see the .ink file's inspector)"
                    : !file.isMaster ? "it is only INCLUDEd by another file; use the master file"
                    : "it has not been compiled";
                Debug.LogError($"{name}: cannot play '{file.name}': {reason}.", file);
                return null;
            }

            return new Story(file.storyJson);
        }

        InkFile Find(string fileName)
        {
            foreach (InkFile file in _inkFiles)
            {
                if (file != null && file.name == fileName)
                    return file;
            }
            return null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            var seen = new HashSet<string>();
            foreach (InkFile file in _inkFiles)
            {
                if (file != null && !seen.Add(file.name))
                    Debug.LogWarning($"{name}: duplicate ink file name '{file.name}'; only the first is used.", this);
            }

            if (_openingStory != null && !_inkFiles.Contains(_openingStory))
                Debug.LogWarning($"{name}: opening story '{_openingStory.name}' is not in the list; the first file is used instead.", this);
        }
#endif
    }
}
