using System;

namespace Hsinpa.Director
{
    /// <summary>
    /// The press that moves a cutscene line on: the UI/Advance action of InputSystem_Actions
    /// (Space, Enter, left click, gamepad south, touch). Rebind it in the input asset, not here.
    /// It is its own action in the UI map, so it works while the Player map is turned off for a cutscene.
    /// </summary>
    sealed class AdvanceInput : IDisposable
    {
        readonly InputSystem_Actions _actions = new InputSystem_Actions();

        public bool WasPressedThisFrame => _actions.UI.Advance.WasPressedThisFrame();

        public void Enable() => _actions.UI.Advance.Enable();
        public void Disable() => _actions.UI.Advance.Disable();
        public void Dispose() => _actions.Dispose();
    }
}
