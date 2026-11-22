using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hsinpa.UIWidget
{
    public class DialogueUIWidget : MonoBehaviour
    {
        public enum AvatarSide
        {
            Left, Right
        }

        [SerializeField]
        private CanvasGroup canvas_group;

        [SerializeField]
        private RectTransform header_object;

        [SerializeField]
        private TextMeshProUGUI header_text;

        [SerializeField]
        private Image left_avatar_img;

        [SerializeField]
        private Image right_avatar_img;

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
                header_object.gameObject.SetActive(false);
            }
            else
            {
                header_object.gameObject.SetActive(true);
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

        public void SetAvatar(Sprite avatar, AvatarSide avatarSide = AvatarSide.Left)
        {


            if (avatarSide == AvatarSide.Left)
            {
                header_object.anchoredPosition = new Vector2(84, header_object.anchoredPosition.y);
            } else
            {
                header_object.anchoredPosition = new Vector2(1164, header_object.anchoredPosition.y);
            }

            if (avatar == null)
            {
                left_avatar_img.gameObject.SetActive(false);
                right_avatar_img.gameObject.SetActive(false);
                return;
            }

            var side_avatar = (avatarSide == AvatarSide.Left) ? left_avatar_img : right_avatar_img;
            side_avatar.sprite = avatar;
            side_avatar.gameObject.SetActive(true);
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

        public void Show(bool is_show=true)
        {
            // Scene teardown destroys objects in no set order; skip if any part of the box is already gone.
            if (IsTornDown()) return;

            Clear();
            canvas_group.alpha = (is_show) ? 1 : 0;
        }

        // Unity's == null is also true for destroyed objects.
        private bool IsTornDown() =>
            this == null || canvas_group == null || header_object == null || header_text == null
            || left_avatar_img == null || right_avatar_img == null || dialogue_text == null;

        public void Clear()
        {
            SetHeader(string.Empty);
            SetAvatar(null);
            SetDialougeText(string.Empty);  
        }
    }
}
