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
        private float text_animate_speed = 0.05f;

        private string target_dialogue_text;

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

            if (!is_animate)
            {
                dialogue_text.text = dialogue;                
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
            // avatar_holder.GetComponent<UnityEngine.UI.Image>().sprite = avatar;        
        }

        public void Clear()
        {
            SetHeader(string.Empty);
            SetAvatar(null);
            SetDialougeText(string.Empty);  
        }
    }
}
