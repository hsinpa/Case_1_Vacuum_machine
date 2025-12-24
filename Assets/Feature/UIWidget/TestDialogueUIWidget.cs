using System;
using TMPro;
using UnityEngine;

namespace Hsinpa.UIWidget
{
    public class TestDialogueUIWidget : MonoBehaviour
    {
        [SerializeField]
        private DialogueUIWidget dialogue_ui_widget;

        [SerializeField]
        private string target_word;

        private void Start()
        {
            dialogue_ui_widget.SetDialougeText(target_word, is_animate: true);
        }

    }
}