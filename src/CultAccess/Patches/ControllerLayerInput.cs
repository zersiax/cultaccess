using CultAccess.Input;
using HarmonyLib;

namespace CultAccess.Patches
{
    /// <summary>
    /// Hides the pad from the game while the accessibility layer is held.
    ///
    /// Without this every press in the layer fires twice — once as a mod command and once as
    /// whatever the game has on that element, which on this pad is always something: A is
    /// Interact, B is Dodge, X is Attack, the D-pad is movement. Pressing the layer's "next
    /// target" would also roll you.
    ///
    /// <c>InputSource</c> is the single funnel: every category source inherits these four and
    /// every button and axis in the game goes through them, so suppressing here covers
    /// gameplay, menus and photo mode at once without enumerating actions. Patching the base
    /// declarations rather than the subclasses is also what Harmony requires.
    ///
    /// Movement is deliberately left alone. The layer takes the buttons, not the character:
    /// the axis patch strips out the D-pad's contribution, which the layer has claimed, and
    /// hands back the analogue stick unchanged so you can still walk while using it.
    /// </summary>
    [HarmonyPatch]
    internal static class ControllerLayerInput
    {
        /// <summary>
        /// Rewired action ids for the axes the D-pad feeds. Gameplay and UI are separate
        /// actions on the same physical elements, and both have to be stripped: without the UI
        /// pair, stepping a target filter inside a menu would also move the menu's own
        /// selection, which is the same double-fire the button suppression exists to prevent.
        /// </summary>
        private const int GameplayHorizontal = 1;
        private const int GameplayVertical = 0;
        private const int UiHorizontal = 35;
        private const int UiVertical = 34;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InputSource), nameof(InputSource.GetButtonDown),
            new[] { typeof(int), typeof(PlayerFarming) })]
        private static void SuppressButtonDown(ref bool __result)
        {
            if (ControllerLayer.Held) __result = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InputSource), "GetButtonHeld",
            new[] { typeof(int), typeof(PlayerFarming) })]
        private static void SuppressButtonHeld(ref bool __result)
        {
            if (ControllerLayer.Held) __result = false;
        }

        /// <summary>
        /// Also suppressed, and not only for symmetry. A button pressed before the trigger
        /// went down would otherwise deliver its release into the game with no matching press,
        /// and several of the game's inputs are release-triggered — <c>GetDodgeRollButtonDown</c>
        /// is literally <c>GetButtonUp</c>.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(InputSource), "GetButtonUp",
            new[] { typeof(int), typeof(PlayerFarming) })]
        private static void SuppressButtonUp(ref bool __result)
        {
            if (ControllerLayer.Held) __result = false;
        }

        /// <summary>
        /// The D-pad is bound to Horizontal and Vertical as well as to Drum1 through 3, so it
        /// feeds the movement axes rather than only the button funnel. Stepping a target list
        /// with the layer would walk the Lamb sideways if this were left alone.
        ///
        /// Replacing the axis with the analogue stick's own value removes the D-pad's
        /// contribution exactly, rather than zeroing movement outright, which is what keeps
        /// walking available while the layer is open.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(InputSource), "GetAxis",
            new[] { typeof(int), typeof(PlayerFarming) })]
        private static void StickOnlyAxis(int axis, ref float __result)
        {
            if (!ControllerLayer.Held) return;

            if ((axis == GameplayHorizontal || axis == UiHorizontal) &&
                ControllerLayer.TryReadStick(horizontal: true, out var horizontal))
                __result = horizontal;
            else if ((axis == GameplayVertical || axis == UiVertical) &&
                     ControllerLayer.TryReadStick(horizontal: false, out var vertical))
                __result = vertical;
        }
    }
}
