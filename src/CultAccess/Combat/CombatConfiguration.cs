using BepInEx.Configuration;
using UnityEngine;

namespace CultAccess.Combat
{
    /// <summary>
    /// Combat settings that are not themselves sounds.
    ///
    /// The cue switches and volumes that used to live here now come from
    /// <c>Audio.AudioConfiguration</c>, which generates one of each per cue. What is left is
    /// the behaviour around them: whether combat state is spoken, how far ahead to predict,
    /// and whether the combat diagnostics are written.
    /// </summary>
    internal static class CombatConfiguration
    {
        private const float MinimumWarningHorizonSeconds = 0.1f;

        internal static ConfigEntry<bool> AnnounceCombatState;
        internal static ConfigEntry<float> WarningHorizon;
        internal static ConfigEntry<bool> LogCombat;
        internal static ConfigEntry<float> MeleeReaction;

        internal static void Bind(ConfigFile config)
        {
            AnnounceCombatState = config.Bind("Combat", "AnnounceCombatState", true,
                "Speak combat start with the enemy count, and announce when the room is clear.");
            WarningHorizon = config.Bind(
                "Combat", "ProjectileWarningHorizonSeconds", 1f,
                "How far ahead to warn about a predicted projectile collision. Raise this if " +
                "warnings arrive too late; lower it if they are too eager.");
            MeleeReaction = config.Bind("Combat", "MeleeCueReactionSeconds", 0.18f,
                "How long before the ideal dodge moment the melee warning sounds, to allow " +
                "for hearing it and pressing the button. The cue is deliberately not played " +
                "when the enemy commits: a wind-up lasts 0.5 seconds and a dodge only " +
                "protects for 0.3, so dodging the instant the attack starts leaves you " +
                "exposed when it lands. The cue is timed so that reacting to it puts the " +
                "dodge over the impact. Raise this if you are consistently dodging late, " +
                "lower it if you are dodging early.");

            LogCombat = config.Bind("Diagnostics", "LogCombat", true,
                "Log dodge corridors, wall contacts, predicted threats, grenade timing, trap " +
                "telegraphs, and actual or evaded player damage. Log only, never spoken.");

            void ApplyHorizon() =>
                ProjectileThreatMonitor.WarningHorizonSeconds =
                    Mathf.Max(MinimumWarningHorizonSeconds, WarningHorizon.Value);

            CombatLifecycle.Announce = AnnounceCombatState.Value;
            MeleeWarningSchedule.ReactionAllowance = MeleeReaction.Value;
            ApplyHorizon();
            CombatDiagnostics.Enabled = LogCombat.Value;

            AnnounceCombatState.SettingChanged +=
                (_, __) => CombatLifecycle.Announce = AnnounceCombatState.Value;
            WarningHorizon.SettingChanged += (_, __) => ApplyHorizon();
            MeleeReaction.SettingChanged +=
                (_, __) => MeleeWarningSchedule.ReactionAllowance = MeleeReaction.Value;
            LogCombat.SettingChanged += (_, __) => CombatDiagnostics.Enabled = LogCombat.Value;
        }
    }
}
