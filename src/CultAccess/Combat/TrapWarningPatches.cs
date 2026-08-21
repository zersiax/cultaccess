using HarmonyLib;
using UnityEngine;

namespace CultAccess.Combat
{
    /// <summary>
    /// Warnings for static hazards: things that sit still and hurt you when you walk onto
    /// them, rather than chasing you.
    ///
    /// These were the last common damage source with no cue at all. The damage telemetry
    /// recorded an attacker that resolved only as <c>GameObject:Sprite</c>, which is
    /// consistent with a trap passing its own object to <c>DealDamage</c>, and the cue
    /// added here is what will confirm or refute that the source really was the spike trap.
    /// </summary>
    ///
    /// <remarks>
    /// Spike traps are worth cueing precisely because they are fair: the game gives a real
    /// telegraph before any damage, so a warning restores information a sighted player
    /// already has rather than granting anything new.
    /// </remarks>
    [HarmonyPatch(typeof(TrapSpikes), "DoAttack")]
    internal static class SpikeTrapWarningPatch
    {
        /// <summary>
        /// The trap's own wind-up, read from <c>TrapSpikes.DoAttack</c>: the spikes shake for
        /// this long before the state becomes Attacking and the overlap check can deal damage.
        ///
        /// A literal because it is a literal in the game's compiled coroutine, with no field
        /// or property exposing it. Re-check this after a game update; the cue still fires at
        /// the right instant if it changes, because it is triggered by the call rather than
        /// calculated from this number. Only the logged lead time would be wrong.
        /// </summary>
        private const float TelegraphSeconds = 0.5f;

        /// <summary>
        /// A prefix on an iterator method runs when the enumerator is created, which is the
        /// same statement that starts the coroutine — so this fires at the exact moment the
        /// trap commits, before any of the wind-up has elapsed.
        /// </summary>
        [HarmonyPrefix]
        private static void BeforeTelegraph(TrapSpikes __instance)
        {
            if (__instance == null) return;
            CombatAssist.WarnStaticHazard(__instance.gameObject, TelegraphSeconds, "spike-trap");
        }
    }
}
