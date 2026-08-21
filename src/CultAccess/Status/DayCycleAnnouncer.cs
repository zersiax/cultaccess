using System.Collections.Generic;
using CultAccess.Speech;
using CultAccess.Util;

namespace CultAccess.Status
{
    /// <summary>
    /// Surfaces the day and night cycle, which the game shows only through
    /// <c>HUD_GameTime</c>'s labels and, for the sermon, a light on the Temple door.
    ///
    /// Deliberately quiet: the day rollover and nightfall are announced, and nothing else.
    /// A survey of what actually depends on time found that ordinary phase changes govern
    /// ambience — lighting, critters, weather, follower task switching — while the things
    /// the player can act on reset per day. Narrating five phases a day would cost an
    /// interruption every ninety-odd seconds to say almost nothing.
    /// </summary>
    internal static class DayCycleAnnouncer
    {
        /// <summary>
        /// Real seconds per in-game minute, derived from the game's own paired constants
        /// <c>REAL_SECONDS_PER_DAY</c> (480) and <c>GAME_MINUTES_PER_DAY</c> (1200) rather
        /// than measured. Spoken values say "about" accordingly.
        /// </summary>
        private const float RealSecondsPerGameMinute =
            (float)TimeManager.REAL_SECONDS_PER_DAY / TimeManager.GAME_MINUTES_PER_DAY;

        private static bool _subscribed;
        private static bool _hasBaseline;
        private static int _lastDay;
        private static DayPhase _lastPhase = DayPhase.None;

        internal static void Initialize()
        {
            if (_subscribed) return;

            try
            {
                TimeManager.OnNewDayStarted += HandleNewDay;
                TimeManager.OnNewPhaseStarted += HandleNewPhase;
                _subscribed = true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not subscribe to time events: {e.Message}");
            }
        }

        /// <summary>
        /// Establishes the loaded save's day and phase silently, so resuming a game never
        /// narrates a rollover that did not happen.
        /// </summary>
        internal static void Tick()
        {
            if (_hasBaseline) return;

            try
            {
                if (DataManager.Instance == null) return;
                _lastDay = TimeManager.CurrentDay;
                _lastPhase = TimeManager.CurrentPhase;
                _hasBaseline = true;
                Plugin.Log.LogInfo(
                    $"[day cycle] baseline day={_lastDay} phase={_lastPhase}");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not read the current day: {e.Message}");
            }
        }

        /// <summary>Day, phase and time to the next phase, for the where-am-I key.</summary>
        internal static string CurrentStatus()
        {
            try
            {
                if (DataManager.Instance == null) return string.Empty;

                var remaining =
                    TimeManager.TimeRemainingInCurrentPhase() * RealSecondsPerGameMinute;
                return DayCycleText.Status(
                    TimeManager.CurrentDay,
                    PhaseName(TimeManager.CurrentPhase),
                    PhaseName(TimeManager.NextPhase),
                    remaining);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not read the day cycle: {e.Message}");
                return string.Empty;
            }
        }

        internal static void Shutdown()
        {
            if (_subscribed)
            {
                try
                {
                    TimeManager.OnNewDayStarted -= HandleNewDay;
                    TimeManager.OnNewPhaseStarted -= HandleNewPhase;
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning($"Could not detach time events: {e.Message}");
                }
                _subscribed = false;
            }

            _hasBaseline = false;
            _lastPhase = DayPhase.None;
        }

        private static void HandleNewDay()
        {
            try
            {
                var day = TimeManager.CurrentDay;
                if (!_hasBaseline)
                {
                    _lastDay = day;
                    _hasBaseline = true;
                    return;
                }

                if (day == _lastDay) return;
                _lastDay = day;

                var restored = RestoredActions();
                var spoken = DayCycleText.NewDay(day, restored);
                Plugin.Log.LogInfo(
                    $"[day cycle] new-day day={day} restored=\"{string.Join(",", restored.ToArray())}\" " +
                    $"spoken=\"{spoken}\"");

                if (Announcing) Speaker.Say(spoken, SpeechPriority.Queued);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"New-day announcement failed: {e.Message}");
            }
        }

        private static void HandleNewPhase()
        {
            try
            {
                var phase = TimeManager.CurrentPhase;
                var previous = _lastPhase;
                _lastPhase = phase;

                // Only nightfall. Every other transition is ambience, and saying them all
                // would interrupt roughly every ninety seconds to convey nothing actionable.
                if (phase != DayPhase.Night || previous == DayPhase.Night) return;

                var spoken = DayCycleText.Nightfall();
                Plugin.Log.LogInfo($"[day cycle] nightfall day={TimeManager.CurrentDay}");
                if (Announcing) Speaker.Say(spoken, SpeechPriority.Queued);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Phase announcement failed: {e.Message}");
            }
        }

        /// <summary>
        /// Once-per-day actions the rollover has just made available again. Only gates read
        /// from the game's own state belong here; a guessed one would send the player to a
        /// refusal.
        /// </summary>
        private static List<string> RestoredActions()
        {
            var restored = new List<string>();

            try
            {
                // Interaction_TempleAltar's exact gate, and the game's only indicator for it
                // is a light on the Temple door. PreviousSermonDayIndex starts at -1, so it
                // reads "available" before a Temple exists; HasBuiltTemple1 is the game's own
                // record that one was ever built.
                var data = DataManager.Instance;
                if (data != null && data.HasBuiltTemple1 &&
                    data.PreviousSermonDayIndex < TimeManager.CurrentDay)
                    restored.Add("Sermon");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not evaluate restored daily actions: {e.Message}");
            }

            return restored;
        }

        private static bool Announcing =>
            Plugin.SpeechEnabled.Value && Plugin.AnnounceDayCycle.Value;

        private static string PhaseName(DayPhase phase) =>
            phase == DayPhase.None || phase == DayPhase.Count
                ? string.Empty
                : RichText.Humanise(phase.ToString());
    }
}
