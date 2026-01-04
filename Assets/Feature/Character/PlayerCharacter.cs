using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hsinpa.Character
{
    public class PlayerCharacter : MonoBehaviour
    {
        [SerializeField]
        Animator _character_animator;

        [SerializeField, Range(0.1f, 10f)]
        private float _move_speed = 1f;

        InputSystem_Actions.PlayerActions _playerActions;
        int input_axis_x;
        int input_axis_y;
        Vector2 input_axis;

        Vector2Int current_position;
        Vector2 offset_position = new Vector2(0.5f, 0.5f);
        Vector2Int target_position;
        Vector3 real_target_position;
        bool is_idle = true;

        void Start()
        { 
            var input_actions = new InputSystem_Actions();
            _playerActions = input_actions.Player;
            _playerActions.Enable();

            _playerActions.Move.performed += OnCharacterMovePerform;
            _playerActions.Move.canceled += OnCharacterMoveCancel;

            current_position = new Vector2Int(Mathf.RoundToInt(transform.position.x - offset_position.x), Mathf.RoundToInt(transform.position.z - offset_position.y));
            transform.position = new Vector3(current_position.x + offset_position.x, current_position.y + offset_position.y, 0);
        }

        void Update()
        {
            if (is_idle) return;

            var move_direction = (real_target_position - transform.position).normalized;
            var velocity = move_direction *_move_speed * Time.deltaTime;
            transform.position = new Vector3(transform.position.x + velocity.x, transform.position.y + velocity.y, 0);

            if (Vector2.Distance(transform.position, real_target_position) < 0.05f)
            {
                current_position.Set(Mathf.RoundToInt(target_position.x), Mathf.RoundToInt(target_position.y));
                transform.position = real_target_position;
                is_idle = true;
                CheckIfCharacterContinueMoving();
                return;
            }
        }

        void OnCharacterMovePerform(InputAction.CallbackContext context)
        {      
            input_axis = context.ReadValue<Vector2>();

            input_axis_x = Mathf.RoundToInt(input_axis.x);
            input_axis_y = Mathf.RoundToInt(input_axis.y);

            CheckIfCharacterContinueMoving();
        }

        void OnCharacterMoveCancel(InputAction.CallbackContext context)
        {   
            input_axis = context.ReadValue<Vector2>();
            input_axis_x = Mathf.RoundToInt(input_axis.x);
            input_axis_y = Mathf.RoundToInt(input_axis.y);
        }

        void CharacterAnimationPoseChange()
        {
            _character_animator.SetFloat("Axis_X", input_axis_x);
            _character_animator.SetFloat("Axis_Y", input_axis_y);
        }

        void CheckIfCharacterContinueMoving()
        {
            if (input_axis.magnitude <= 0.1)
            {
                is_idle = true;
                _character_animator.SetFloat("Speed", 0);
                return;
            }

            if (!is_idle) return;

            target_position.Set(current_position.x, current_position.y);

            if (input_axis_x > 0.9f || input_axis_x < - 0.9f)
            {
                target_position.x = current_position.x + input_axis_x;
            } else
            {
                target_position.y = current_position.y + input_axis_y ;
            }

            GetRealPosition(target_position, ref real_target_position);
            CharacterAnimationPoseChange();

            _character_animator.SetFloat("Speed", 1);
            is_idle = false;
        }

        Vector3 GetRealPosition(Vector2Int in_game_position, ref Vector3 source)
        {
            source.x = Mathf.RoundToInt(in_game_position.x) + offset_position.x;
            source.y = Mathf.RoundToInt(in_game_position.y) + offset_position.y;
            source.z = 0;
            return source;
        }
    }   
}