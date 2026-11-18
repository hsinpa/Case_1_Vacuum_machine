using System;
using UnityEngine;

namespace Hsinpa.Character
{
    /// <summary>
    /// Wraps the generated input actions: the held move direction as a single grid step,
    /// and pointer clicks in screen space. Knows nothing about cameras or the grid.
    /// </summary>
    class PlayerInputReader : IDisposable
    {
        const float AxisThreshold = 0.5f;

        readonly InputSystem_Actions _inputActions;

        public PlayerInputReader()
        {
            _inputActions = new InputSystem_Actions();
            _inputActions.Player.Enable();
        }

        /// <summary>
        /// Currently held direction as one of 8 grid steps: each axis is -1, 0 or 1.
        /// Vector2Int.zero when nothing is held.
        /// </summary>
        public Vector2Int Direction
        {
            get
            {
                Vector2 axis = _inputActions.Player.Move.ReadValue<Vector2>();
                return new Vector2Int(ToStep(axis.x), ToStep(axis.y));

                static int ToStep(float value) =>
                    Mathf.Abs(value) > AxisThreshold ? (value > 0 ? 1 : -1) : 0;
            }
        }

        /// <summary>
        /// True only on the frame the click (mouse left button or touch) is pressed.
        /// screenPosition is the pointer position in pixels at that moment.
        /// </summary>
        public bool TryGetClick(out Vector2 screenPosition)
        {
            screenPosition = default;
            if (!_inputActions.Player.Click.WasPressedThisFrame()) return false;

            screenPosition = _inputActions.Player.Point.ReadValue<Vector2>();
            return true;
        }

        public void Dispose()
        {
            _inputActions.Player.Disable();
            _inputActions.Dispose();
        }
    }
}
