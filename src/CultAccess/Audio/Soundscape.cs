using System.Collections.Generic;
using CultAccess.Combat;
using UnityEngine;

namespace CultAccess.Audio
{
    /// <summary>
    /// The always-on layer: quiet, repeating cues for the things around the player, so the
    /// world can be heard rather than only queried.
    ///
    /// The mod's original navigation model is on demand — press a key, hear a list. That is
    /// precise and it is slow, and it cannot answer the questions that come up while moving:
    /// is anything on my left, did that enemy get closer, how many shots are in the air. This
    /// layer answers those continuously and answers nothing else, leaving the spoken scanner
    /// as the way to get names and exact states.
    ///
    /// Three rules keep it from becoming noise, and all three came from the requirement that
    /// it be usable for orientation rather than merely present:
    ///
    /// - **Every category is off until the player turns it on.** The mod sounds exactly as it
    ///   did before this existed until someone opts in, one category at a time.
    /// - **Distance is carried by volume, steeply.** Things are inaudible until close, so an
    ///   empty room is silent and a wall alongside you is obvious. See
    ///   <see cref="SoundscapeFalloff"/>.
    /// - **There is a hard ceiling on how much can sound.** Per category, and across all of
    ///   them together, because a boss arena can put fifty objects inside the radius.
    /// </summary>
    internal static class Soundscape
    {
        /// <summary>
        /// The order categories claim the shared budget in, most important first. When a room
        /// is busy enough to exhaust it, the player keeps hearing incoming fire and loses the
        /// furniture, which is the only sensible way round.
        /// </summary>
        private static readonly CueId[] Priority =
        {
            CueId.AmbientProjectile,
            CueId.AmbientEnemy,
            CueId.AmbientWall,
            CueId.AmbientItem,
            CueId.AmbientInteractable,
            CueId.AmbientNpc,
        };

        private static readonly Dictionary<CueId, SoundscapeSettings> Tuning =
            new Dictionary<CueId, SoundscapeSettings>();
        private static readonly Dictionary<CueId, float> NextPlayAt = new Dictionary<CueId, float>();
        private static readonly List<Vector3> Positions = new List<Vector3>(16);

        /// <summary>
        /// Total ambient sounds allowed per second across every category. Reached only in a
        /// genuinely crowded room; its purpose is to make the worst case bounded rather than
        /// to shape ordinary play.
        /// </summary>
        internal static int MaxPingsPerSecond = 40;

        private static float _budgetWindowEndsAt;
        private static int _budgetSpent;
        private static bool _loggedFirstTick;

        static Soundscape()
        {
            foreach (var cue in Priority)
            {
                Tuning[cue] = SoundscapeSettings.Default(cue);
                NextPlayAt[cue] = 0f;
            }
        }

        internal static SoundscapeSettings Settings(CueId cue) =>
            Tuning.TryGetValue(cue, out var settings) ? settings : SoundscapeSettings.Default(cue);

        /// <summary>
        /// Check that every ambient cue appears in <see cref="Priority"/>, and report by name
        /// if one does not.
        ///
        /// A category missing from that list gets a fresh settings object from every call to
        /// <see cref="Settings"/>, so the config file would appear to configure it while every
        /// write was silently discarded, and it would never be ticked at all. The list is
        /// hand-written because its order decides who keeps sounding when a room is too busy
        /// for all of them, so it cannot be generated - which makes checking it the only way
        /// to know it is right.
        /// </summary>
        internal static bool Validate(System.Action<string> report)
        {
            var complete = true;

            foreach (var cue in Cues.All)
            {
                if (!Cues.IsAmbient(cue) || Tuning.ContainsKey(cue)) continue;

                complete = false;
                report($"Always-on cue {cue} is missing from the soundscape priority list, so " +
                       "its settings would be discarded and it would never play.");
            }

            return complete;
        }

        /// <summary>True when at least one category is switched on and could sound.</summary>
        internal static bool AnyEnabled
        {
            get
            {
                foreach (var cue in Priority)
                    if (CueSettings.Enabled(cue))
                        return true;

                return false;
            }
        }

        /// <summary>
        /// Stop anything that sustains. The pulsed categories need no stopping - they simply
        /// are not started again - but the wall ring does.
        /// </summary>
        internal static void Silence() => WallSonar.Reset();

        internal static void Reset()
        {
            foreach (var cue in Priority)
                NextPlayAt[cue] = 0f;

            _budgetSpent = 0;
            _budgetWindowEndsAt = 0f;
            WallSonar.Reset();
        }

        /// <summary>
        /// Driven from the plugin's update. Returns immediately when nothing is switched on,
        /// which is the default state, so the layer costs nothing until it is wanted.
        /// </summary>
        internal static void Tick()
        {
            // Every exit below goes through Silence(), never a bare return. The wall ring
            // holds continuous FMOD channels open, so an early return that skipped stopping
            // them would leave a drone running for the rest of the session - through death,
            // through a cutscene, through the pause menu. This is the one place in the mod
            // where forgetting to clean up is audible forever rather than merely wrong.
            if (!AnyEnabled) { Silence(); return; }

            var player = PlayerFarming.Instance;
            if (player == null || !player.gameObject.activeInHierarchy) { Silence(); return; }
            if (Time.timeScale <= 0f) { Silence(); return; }
            if (!CombatAssist.PlayerCanReceiveRealtimeCues(player)) { Silence(); return; }

            if (!_loggedFirstTick)
            {
                _loggedFirstTick = true;
                Plugin.Log.LogInfo("[soundscape] first tick with at least one category enabled");
            }

            RefreshBudget();

            if (!CueSettings.Enabled(CueId.AmbientWall)) WallSonar.Reset();

            var origin = player.transform.position;
            foreach (var cue in Priority)
            {
                if (!CueSettings.Enabled(cue)) continue;

                var settings = Settings(cue);
                if (cue == CueId.AmbientWall)
                {
                    // Geometry has no object list to walk; it is probed rather than gathered,
                    // and it sustains rather than repeating, so it neither uses the repeat
                    // timer nor draws on the per-second budget.
                    WallSonar.Tick(player, settings);
                    continue;
                }

                if (Time.unscaledTime < NextPlayAt[cue]) continue;
                NextPlayAt[cue] = Time.unscaledTime + Mathf.Max(0.05f, settings.Interval);

                PlayCategory(cue, settings, origin);
            }
        }

        private static void PlayCategory(CueId cue, SoundscapeSettings settings, Vector3 origin)
        {
            SoundscapeSources.Collect(
                cue, origin, settings.Radius, settings.MaxSources, Positions);

            foreach (var position in Positions)
            {
                if (_budgetSpent >= MaxPingsPerSecond) return;

                var distance = Vector3.Distance(origin, position);
                var volume = SoundscapeFalloff.Volume(distance, settings.Radius, settings.Falloff);
                if (!SoundscapeFalloff.Audible(volume)) continue;

                if (CuePlayer.Play(cue, position, volume)) _budgetSpent++;
            }
        }

        private static void RefreshBudget()
        {
            if (Time.unscaledTime < _budgetWindowEndsAt) return;

            if (_budgetSpent >= MaxPingsPerSecond)
                Plugin.Log.LogInfo(
                    $"[soundscape] budget reached: {_budgetSpent} ambient cues in the last second");

            _budgetWindowEndsAt = Time.unscaledTime + 1f;
            _budgetSpent = 0;
        }
    }
}
