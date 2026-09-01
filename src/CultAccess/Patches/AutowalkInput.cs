using CultAccess.Navigation;
using HarmonyLib;

namespace CultAccess.Patches
{
    /// <summary>
    /// The whole of autowalk's contact with the game: while
    /// <see cref="Autowalk.Driving"/> holds, the two gameplay movement axes report the
    /// heading guidance is announcing instead of the stick.
    ///
    /// Substituting the axes rather than moving the Lamb is what keeps this honest. Speed,
    /// acceleration, collision, facing, the analogue speed curve and the game's own
    /// invert-movement setting are all downstream of these two numbers, so none of them has
    /// to be reimplemented and none of them can drift from what the game does for a player
    /// holding a direction.
    ///
    /// Substituted after the fact, in a postfix, so the accessibility invert the game applies
    /// inside these methods cannot flip a heading that is already in world terms.
    ///
    /// These are the declared methods on <c>RewiredGameplayInputSource</c> rather than the
    /// inherited <c>InputSource.GetAxis</c>, which Harmony would refuse, and which every
    /// other axis in the game also goes through.
    ///
    /// One side effect worth stating rather than discovering. These are the gameplay axes,
    /// not the walking axes, so anything else reading them while the Lamb is walking sees the
    /// heading too. <see cref="Autowalk"/> only drives in the six states where the axes mean
    /// walking, which keeps the aim direction, the building cursor, the map pan and the
    /// fishing reel out of it; what remains is <c>EnemyExploder</c>, which biases its lock-on
    /// by the player's current input. While autowalk is on, a struck exploder will favour the
    /// route heading — the same thing it would do for a player holding that direction.
    /// </summary>
    [HarmonyPatch]
    internal static class AutowalkInput
    {
        /// <summary>
        /// Skip the whole class rather than let a renamed method abort <c>PatchAll</c>.
        /// Harmony applies patch classes in sequence, so one missing target after a game
        /// update would otherwise take the classes behind it down with it.
        /// </summary>
        private static bool Prepare()
        {
            var horizontal = AccessTools.Method(
                typeof(RewiredGameplayInputSource),
                nameof(RewiredGameplayInputSource.GetHorizontalAxis));
            var vertical = AccessTools.Method(
                typeof(RewiredGameplayInputSource),
                nameof(RewiredGameplayInputSource.GetVerticalAxis));

            if (horizontal != null && vertical != null) return true;

            Plugin.Log.LogWarning(
                "Gameplay movement axes not found on RewiredGameplayInputSource; autowalk is " +
                "disabled for this session. Every other accessibility feature is unaffected.");
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(RewiredGameplayInputSource),
            nameof(RewiredGameplayInputSource.GetHorizontalAxis))]
        private static void SupplyHorizontalAxis(PlayerFarming playerFarming, ref float __result)
        {
            if (Autowalk.Driving && IsGuidedPlayer(playerFarming))
                __result = Autowalk.Horizontal;
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(RewiredGameplayInputSource),
            nameof(RewiredGameplayInputSource.GetVerticalAxis))]
        private static void SupplyVerticalAxis(PlayerFarming playerFarming, ref float __result)
        {
            if (Autowalk.Driving && IsGuidedPlayer(playerFarming))
                __result = Autowalk.Vertical;
        }

        /// <summary>
        /// Only the player guidance is running for. The argument is the one the caller passed,
        /// before the method body substitutes <c>PlayerFarming.Instance</c> for a null, so a
        /// null here means the same player; a second local player in co-op arrives as their
        /// own instance and must keep their own stick.
        /// </summary>
        private static bool IsGuidedPlayer(PlayerFarming playerFarming) =>
            playerFarming == null || playerFarming == PlayerFarming.Instance;
    }
}
