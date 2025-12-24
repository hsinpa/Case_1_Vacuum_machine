using System;
using TMPro;
using UnityEngine;

namespace Hsinpa.UIWidget
{
    public class DialogueUIWidget : MonoBehaviour
    {
        [SerializeField]
        private GameObject header_object;

        [SerializeField]
        private TextMeshProUGUI header_text;

        [SerializeField]
        private GameObject avatar_holder;

        [SerializeField]
        private TextMeshProUGUI dialogue_text;

        [SerializeField, Range(0.01f, 0.5f)]
        private float text_animate_interval = 0.05f;
        private float record_animate_time = 0f;

        private string target_dialogue_text;
        private int current_text_index = 0;
        private bool animation_toggle = false;

        public void SetHeader(string header)
        {
            if (string.IsNullOrEmpty(header))
            {
                header_object.SetActive(false);
            }
            else
            {
                header_object.SetActive(true);
                header_text.text = header;
            }
        }

        public void SetDialougeText(string dialogue, bool is_animate = false)
        {
            target_dialogue_text = dialogue;
            animation_toggle = is_animate;

            dialogue_text.text = "";
            current_text_index = 0;

            if (!is_animate)
            {
                dialogue_text.text = dialogue;                
            }
        }

        public void SetAnimateInterval(float interval)
        {
            text_animate_interval = interval;

            // Instant complete
            if (interval == 0)
            {
                SetDialougeText(target_dialogue_text);
            }
        }

        public void SetAvatar(Sprite avatar)
        {
            if (avatar == null)
            {
                avatar_holder.SetActive(false);
                return;
            }

            avatar_holder.SetActive(true);
        }

        private void Update()
        {
            if (animation_toggle && current_text_index < target_dialogue_text.Length
            && Time.time > record_animate_time)
            {
                dialogue_text.text = dialogue_text.text + target_dialogue_text.Substring(current_text_index, 1);
                current_text_index++;

                if (current_text_index >= target_dialogue_text.Length) {
                    animation_toggle = false;
                }

                record_animate_time = Time.time + text_animate_interval;
            }
        }

        public void Clear()
        {
            SetHeader(string.Empty);
            SetAvatar(null);
            SetDialougeText(string.Empty);  
        }
    }
}
