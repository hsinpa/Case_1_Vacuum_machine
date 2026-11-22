using Hsinpa.Director;
using UnityEngine;

namespace Hsinpa.UIWidget
{
    /// <summary>
    /// Bridges the Director to the dialogue box: shows each cutscene line the runner raises and hides it
    /// when the runner moves on. Advancing stays the runner's job (UI/Advance); this only displays.
    /// </summary>
    public class DialogueUIPresenter : MonoBehaviour
    {
        [SerializeField]
        private DialogueRunner runner;

        [SerializeField]
        private DialogueUIWidget widget;

        [SerializeField]
        private bool animate_text = true;

        private void Start()
        {
            widget.Show(false);
        }

        private void OnEnable()
        {
            if (runner == null) return;
            runner.LineShown += OnLineShown;
            runner.LineHidden += OnLineHidden;
        }

        private void OnDisable()
        {
            if (runner == null) return;
            runner.LineShown -= OnLineShown;
            runner.LineHidden -= OnLineHidden;
        }

        private void OnLineShown(DialogueLine line)
        {
            if (line.Mode == DialogueMode.Bark) return;   // barks belong to speech bubbles
            if (widget == null) return;                    // destroyed, e.g. while leaving Play mode

            // Show() clears the box, so it must come before the content is set.
            widget.Show(true);
            widget.SetHeader(line.Speaker ?? string.Empty);   // null speaker = narration, header hidden
            widget.SetDialougeText(line.Text, animate_text);
        }

        private void OnLineHidden(DialogueLine line)
        {
            if (line.Mode == DialogueMode.Bark) return;
            if (widget == null) return;   // the runner stops on teardown, possibly after the UI is gone

            widget.Show(false);
        }
    }
}
