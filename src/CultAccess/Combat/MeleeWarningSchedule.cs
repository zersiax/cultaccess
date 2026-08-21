using System.Collections.Generic;
using CultAccess.Audio;
using UnityEngine;

namespace CultAccess.Combat
{
    /// <summary>
    /// Holds a melee warning back until dodging on it actually works.
    ///
    /// The first version fired the moment an enemy committed to its attack, which sounds
    /// right and is wrong. The numbers, all read from the game rather than assumed:
    ///
    /// - `EnemyScuttleSwiper.SignPostAttackDuration` is **0.5 s** — the wind-up before damage.
    /// - `PlayerController.DodgeDuration` is **0.3 s** — how long a dodge lasts if the button
    ///   is released, extended to `DodgeMaxDuration`, 0.5 s, only while it is held.
    /// - Invulnerability covers the whole `Dodging` state: `Health.DealDamage` refuses
    ///   outright while it is current.
    ///
    /// So a player who taps dodge the instant they hear the cue is invulnerable from 0 to 0.3
    /// and is hit at 0.5. Not marginally — they are exposed for the two tenths of a second
    /// that matter. Even a fast reaction of about 0.18 s only reaches 0.18 to 0.48, which
    /// still misses. **Reacting immediately was guaranteed to be too early**, and a cue that
    /// trains the wrong reflex is worse than no cue, because the player trusts it.
    ///
    /// The fix is to move the cue to the moment when reacting to it lands the dodge over the
    /// impact, so "dodge when you hear it" becomes true. Every term is read live, so a game
    /// update that retunes a wind-up or a dodge keeps working without anyone noticing it had
    /// to.
    /// </summary>
    internal static class MeleeWarningSchedule
    {
        private struct Pending
        {
            internal GameObject Source;
            internal float FireAt;
            internal float ImpactAt;
            internal string Family;
        }

        private static readonly List<Pending> Waiting = new List<Pending>(8);

        /// <summary>
        /// Allowance for hearing the cue and pressing the button, in seconds.
        ///
        /// A property of the player rather than of the game, so it cannot be derived from
        /// anything and is exposed as a setting instead. Simple auditory reaction is around
        /// 0.15 to 0.2 s for most people and slower for some; the default sits inside that,
        /// and anyone landing consistently early or late can move it.
        /// </summary>
        internal static float ReactionAllowance = 0.18f;

        internal static void Clear() => Waiting.Clear();

        /// <summary>
        /// Work out when to sound a warning for an attack landing <paramref name="impactIn"/>
        /// seconds from now, and queue it. Fires immediately when the telegraph is too short
        /// to wait in, which is honest: a warning that arrives late is still worth more than
        /// one that never comes.
        /// </summary>
        internal static void Schedule(GameObject source, float impactIn, string family)
        {
            var delay = Delay(impactIn, DodgeCover());
            var now = Time.unscaledTime;

            Waiting.Add(new Pending
            {
                Source = source,
                FireAt = now + delay,
                ImpactAt = now + impactIn,
                Family = family,
            });

            CombatDiagnostics.Info(
                $"[combat melee] scheduled family={family} impactIn={impactIn:0.000} " +
                $"dodgeCover={DodgeCover():0.000} reaction={ReactionAllowance:0.000} " +
                $"delay={delay:0.000}");
        }

        /// <summary>
        /// How long after the enemy commits the cue should sound.
        ///
        /// Aim the middle of the dodge's invulnerability at the impact, then pull the cue
        /// forward by the player's reaction time. Never negative, and never later than the
        /// impact itself.
        /// </summary>
        internal static float Delay(float impactIn, float dodgeCover)
        {
            var idealPress = impactIn - dodgeCover * 0.5f;
            var cue = idealPress - ReactionAllowance;
            return Mathf.Clamp(cue, 0f, Mathf.Max(0f, impactIn));
        }

        internal static void Tick()
        {
            if (Waiting.Count == 0) return;

            var now = Time.unscaledTime;
            for (var i = Waiting.Count - 1; i >= 0; i--)
            {
                var pending = Waiting[i];
                if (now < pending.FireAt) continue;

                Waiting.RemoveAt(i);

                // The attacker can die, despawn or be interrupted during its own wind-up.
                // Warning about an attack that is no longer coming teaches the player to
                // distrust the cue, which costs more than the missed warning would have.
                if (pending.Source == null || !pending.Source.activeInHierarchy)
                {
                    CombatDiagnostics.Info(
                        $"[combat melee] cancelled family={pending.Family} reason=source-gone");
                    continue;
                }

                CombatAssist.SoundMeleeWarning(
                    pending.Source, pending.ImpactAt - now, pending.Family);
            }
        }

        /// <summary>
        /// How long a dodge protects for, read from the live player controller. A tap, not a
        /// hold: holding reaches `DodgeMaxDuration` but requires the player to commit to it,
        /// and a cue must be correct for the ordinary press.
        /// </summary>
        private static float DodgeCover()
        {
            var player = PlayerFarming.Instance;
            var controller = player == null ? null : player.playerController;

            // A sane fallback rather than zero: with no controller to read, an aim of half a
            // dodge is still closer than firing the cue immediately.
            return controller == null ? 0.3f : controller.DodgeDuration;
        }
    }
}
