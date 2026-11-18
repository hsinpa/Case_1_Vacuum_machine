using System.Collections.Generic;
using Hsinpa.Map;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hsinpa.Character
{
    /// <summary>
    /// The character the player controls: turns input into orders for its GridAgent,
    /// and mirrors the agent's state onto the animator.
    /// Keyboard steps one cell at a time; a click walks to the clicked cell, or beside it.
    /// </summary>
    [RequireComponent(typeof(GridAgent))]
    public class PlayerCharacter : MonoBehaviour
    {
        [SerializeField]
        Animator _character_animator;

        [SerializeField]
        TileMarkIndicator _targetTileSprite;

        PlayerInputReader _input;
        GridAgent _agent;
        CharacterAnimationView _view;

        // Reused for UI hit tests so a click allocates nothing.
        PointerEventData _uiPointer;
        EventSystem _uiPointerOwner;
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        bool _warnedNoCamera;

        // Seconds a lone key waits for a second one before stepping straight. See IsWaitingForSecondKey.
        const float DiagonalGraceTime = 0.06f;
        float _keyHeldTime;

        void Awake()
        {
            _agent = GetComponent<GridAgent>();
        }

        void Start()
        {
            _input = new PlayerInputReader();
            _view = new CharacterAnimationView(_character_animator);
        }

        void Update()
        {
            // Keyboard wins over a click in the same frame. TryStep cancels any path,
            // and is ignored while a step is still in progress.
            Vector2Int direction = _input.Direction;
            if (direction == Vector2Int.zero) _keyHeldTime = 0f;
            else _keyHeldTime += Time.deltaTime;

            if (direction != Vector2Int.zero)
            {
                if (!IsWaitingForSecondKey(direction)) _agent.TryStep(direction);
            }
            else if (_input.TryGetClick(out Vector2 screenPosition)
                     && !IsOverUI(screenPosition)
                     && TryScreenToCell(screenPosition, out Vector2Int cell))
            {
                _agent.MoveTo(cell);
                MarkTargetTile(cell);
            }

            _view.Face(_agent.Facing);
            _view.SetMoving(_agent.IsMoving);
        }

        void OnDestroy()
        {
            _input?.Dispose();
        }

        /// <summary>
        /// Two keys for a diagonal rarely land on the same frame. Starting from standing still, a single
        /// held key waits a moment for a second one, so the first step is not a straight step by mistake.
        /// Once keys have been held past the window, steps follow at once until every key is released.
        /// </summary>
        bool IsWaitingForSecondKey(Vector2Int direction)
        {
            bool isDiagonal = direction.x != 0 && direction.y != 0;
            return !isDiagonal && !_agent.IsMoving && _keyHeldTime < DiagonalGraceTime;
        }

        /// <summary>
        /// Moves the marker to the centre of the clicked cell so the player sees where they clicked.
        /// </summary>
        void MarkTargetTile(Vector2Int cell)
        {
            if (_targetTileSprite == null) return;

            _targetTileSprite.Blink(_agent.Map.CellCenterToWorld(cell));
        }

        /// <summary>
        /// Clicks on dialogue boxes and buttons belong to the UI, not the world.
        /// Raycasts the UI at this exact click position instead of using IsPointerOverGameObject,
        /// which reports the EventSystem's last update and is wrong on the first frame of a touch.
        /// </summary>
        bool IsOverUI(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            if (_uiPointer == null || _uiPointerOwner != eventSystem)
            {
                _uiPointer = new PointerEventData(eventSystem);
                _uiPointerOwner = eventSystem;
            }

            _uiPointer.position = screenPosition;
            eventSystem.RaycastAll(_uiPointer, _uiHits);
            return _uiHits.Count > 0;
        }

        /// <summary>
        /// Casts the screen point onto the grid's plane. Works for perspective and orthographic cameras,
        /// unlike ScreenToWorldPoint, which needs the depth to be right for perspective.
        /// </summary>
        bool TryScreenToCell(Vector2 screenPosition, out Vector2Int cell)
        {
            cell = default;

            Camera camera = Camera.main;
            if (camera == null)
            {
                if (!_warnedNoCamera)
                {
                    Debug.LogWarning("Click-to-move needs a camera tagged MainCamera; clicks are ignored until there is one.", this);
                    _warnedNoCamera = true;
                }
                return false;
            }
            if (_agent.Map == null) return false;

            Transform grid = _agent.Map.transform;
            var gridPlane = new Plane(grid.forward, grid.position);
            Ray ray = camera.ScreenPointToRay(screenPosition);

            if (!gridPlane.Raycast(ray, out float distance)) return false;

            cell = _agent.Map.WorldToCell(ray.GetPoint(distance));
            return true;
        }
    }
}
