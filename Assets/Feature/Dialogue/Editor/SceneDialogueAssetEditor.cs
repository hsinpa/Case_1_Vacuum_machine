using System.Collections.Generic;
using Ink.UnityIntegration;
using UnityEditor;
using UnityEngine;

namespace Hsinpa.Dialogue.Editor
{
    /// <summary>Shows the opening story as a dropdown of the asset's own ink files.</summary>
    [CustomEditor(typeof(SceneDialogueAsset))]
    class SceneDialogueAssetEditor : UnityEditor.Editor
    {
        static readonly GUIContent OpeningLabel = new GUIContent("Opening Story", "Played by DialogueRunner.Play(). (First in list) follows the list order.");

        SerializedProperty _inkFiles;
        SerializedProperty _openingStory;
        SerializedProperty _playOnStart;

        void OnEnable()
        {
            _inkFiles = serializedObject.FindProperty("_inkFiles");
            _openingStory = serializedObject.FindProperty("_openingStory");
            _playOnStart = serializedObject.FindProperty("_playOnStart");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_inkFiles);

            // Option 0 is "first in list" (stored as null); the rest are the non-empty entries.
            var files = new List<InkFile>();
            var labels = new List<string> { "(First in list)" };
            for (int i = 0; i < _inkFiles.arraySize; i++)
            {
                if (_inkFiles.GetArrayElementAtIndex(i).objectReferenceValue is InkFile file && !files.Contains(file))
                {
                    files.Add(file);
                    labels.Add(file.name);
                }
            }

            var current = _openingStory.objectReferenceValue as InkFile;
            int selected = current != null ? files.IndexOf(current) + 1 : 0;
            if (selected < 0) selected = 0;   // chosen file was removed from the list

            int picked = EditorGUILayout.Popup(OpeningLabel, selected, labels.ToArray());
            if (picked != selected || (current != null && !files.Contains(current)))
                _openingStory.objectReferenceValue = picked == 0 ? null : files[picked - 1];

            EditorGUILayout.PropertyField(_playOnStart);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
