using UnityEngine;

namespace Hsinpa.Character
{
    /// <summary>
    /// The only place that knows the animator parameter names.
    /// </summary>
    class CharacterAnimationView
    {
        static readonly int AxisX = Animator.StringToHash("Axis_X");
        static readonly int AxisY = Animator.StringToHash("Axis_Y");
        static readonly int Speed = Animator.StringToHash("Speed");

        readonly Animator _animator;

        public CharacterAnimationView(Animator animator)
        {
            _animator = animator;
        }

        public void Face(Vector2Int direction)
        {
            _animator.SetFloat(AxisX, direction.x);
            _animator.SetFloat(AxisY, direction.y);
        }

        public void SetMoving(bool isMoving)
        {
            _animator.SetFloat(Speed, isMoving ? 1f : 0f);
        }
    }
}
