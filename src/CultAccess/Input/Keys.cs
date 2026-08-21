using System;
using UnityEngine;

namespace CultAccess.Input
{
    /// <summary>
    /// Reads raw keyboard state for the mod's own hotkeys.
    ///
    /// Two complications this papers over:
    /// 1. The game drives gameplay input through Rewired, so we stay off that entirely
    ///    and read the keyboard directly — our hotkeys can't collide with rebinding.
    /// 2. A Unity project built with the new Input System package only will throw from
    ///    <see cref="UnityEngine.Input"/>. We detect that once and fall back.
    /// </summary>
    public static class Keys
    {
        private enum Backend { Unknown, Legacy, InputSystem, None }

        private static Backend _backend = Backend.Unknown;

        private static void Detect()
        {
            try
            {
                // Touching anything on the legacy Input class is enough to trip the
                // InvalidOperationException when legacy input is disabled.
                _ = UnityEngine.Input.anyKey;
                _backend = Backend.Legacy;
                Plugin.Log.LogInfo("Hotkey backend: legacy UnityEngine.Input");
                return;
            }
            catch (Exception)
            {
                // fall through
            }

            try
            {
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    _backend = Backend.InputSystem;
                    Plugin.Log.LogInfo("Hotkey backend: Unity Input System");
                    return;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Input System unavailable: {e.Message}");
            }

            _backend = Backend.None;
            Plugin.Log.LogError("No usable keyboard backend — mod hotkeys are dead.");
        }

        public static bool Down(KeyCode key)
        {
            if (_backend == Backend.Unknown) Detect();

            switch (_backend)
            {
                case Backend.Legacy:
                    return UnityEngine.Input.GetKeyDown(key);
                case Backend.InputSystem:
                    return InputSystemDown(key);
                default:
                    return false;
            }
        }

        public static bool Held(KeyCode key)
        {
            if (_backend == Backend.Unknown) Detect();

            switch (_backend)
            {
                case Backend.Legacy:
                    return UnityEngine.Input.GetKey(key);
                case Backend.InputSystem:
                    return InputSystemHeld(key);
                default:
                    return false;
            }
        }

        /// <summary>True while either shift is down — used as a hotkey modifier.</summary>
        public static bool Shift => Held(KeyCode.LeftShift) || Held(KeyCode.RightShift);

        /// <summary>True while either control is down.</summary>
        public static bool Control => Held(KeyCode.LeftControl) || Held(KeyCode.RightControl);

        private static UnityEngine.InputSystem.Controls.KeyControl Map(KeyCode key)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return null;

            // KeyCode and the Input System's Key enum share names for everything we bind,
            // so translate by name rather than maintaining a 100-entry switch.
            if (!Enum.TryParse<UnityEngine.InputSystem.Key>(key.ToString(), out var mapped)) return null;
            return keyboard[mapped];
        }

        private static bool InputSystemDown(KeyCode key)
        {
            var control = Map(key);
            return control != null && control.wasPressedThisFrame;
        }

        private static bool InputSystemHeld(KeyCode key)
        {
            var control = Map(key);
            return control != null && control.isPressed;
        }
    }
}
