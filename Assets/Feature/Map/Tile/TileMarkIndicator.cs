using UnityEngine;

namespace Hsinpa.Map
{
    public class TileMarkIndicator : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer renderer;

        [SerializeField]
        private Animation blink_animation;

        public void Blink(Vector3 position) {
            transform.position = position;
            blink_animation.Stop();
            blink_animation.Play();
        }
    }
}
