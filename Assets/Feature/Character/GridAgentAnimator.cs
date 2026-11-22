using UnityEngine;

namespace Hsinpa.Character
{
    /// <summary>
    /// Mirrors a GridAgent's facing and movement onto an animator, for characters without a
    /// PlayerCharacter (NPCs walked by AI or cutscenes). PlayerCharacter already does this itself,
    /// so do not add both to the same character.
    /// </summary>
    [RequireComponent(typeof(GridAgent))]
    public class GridAgentAnimator : MonoBehaviour
    {
        [SerializeField]
        Animator _animator;

        GridAgent _agent;
        CharacterAnimationView _view;

        void Awake()
        {
            _agent = GetComponent<GridAgent>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null)
            {
                Debug.LogError($"{name}: GridAgentAnimator has no Animator assigned or in children.", this);
                enabled = false;
                return;
            }
            _view = new CharacterAnimationView(_animator);
        }

        void LateUpdate()
        {
            _view.Face(_agent.Facing);
            _view.SetMoving(_agent.IsMoving);
        }
    }
}
