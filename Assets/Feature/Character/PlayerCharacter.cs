using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.InputSystem;


namespace Hsinpa.Character
{
    public class PlayerCharacter : MonoBehaviour
    {
        [SerializeField]
        Animator _character_animator;

        InputSystem_Actions.PlayerActions _playerActions;
        int input_axis_x;
        int input_axis_y;

        void Start()
        { 
            var input_actions = new InputSystem_Actions();
            _playerActions = input_actions.Player;
            _playerActions.Enable();

            _playerActions.Move.performed += OnCharacterMovePerform;
            _playerActions.Move.canceled += OnCharacterMoveCancel;
        }

        void OnCharacterMovePerform(InputAction.CallbackContext context)
        {      
            Vector2 input_axis = context.ReadValue<Vector2>();

            if (input_axis.magnitude <= 0.1f) return;

            input_axis_x = Mathf.RoundToInt(input_axis.x);
            input_axis_y = Mathf.RoundToInt(input_axis.y);

            CharacterAnimationPoseChange();
        }

        void OnCharacterMoveCancel(InputAction.CallbackContext context)
        {   

        }

        void CharacterAnimationPoseChange()
        {
            // Debug.Log($"input_axis_x {input_axis_x}");
            // Debug.Log($"input_axis_y {input_axis_y}");

            _character_animator.SetFloat("Axis_X", input_axis_x);
            _character_animator.SetFloat("Axis_Y", input_axis_y);
        }
    }   
}