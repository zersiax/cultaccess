using System.Collections.Generic;
using CultAccess.Localization;

namespace CultAccess.Status
{
    /// <summary>
    /// Wording for the day and night cycle. Pure, so it is tested without the game.
    ///
    /// The design principle here is that phases are not worth narrating: five per day at 480
    /// real seconds each means a transition roughly every 96 seconds, and almost none of them
    /// change what the player can do. What changes is the day rollover, which restores the
    /// once-per-day actions. So the automatic speech names consequences, and the phase itself
    /// is only ever produced on demand.
    /// </summary>
    internal static class DayCycleText
    {
        /// <summary>
        /// Spoken once per in-game day. The restored actions are the reason this is worth
        /// saying at all, so they lead over any ambient description.
        /// </summary>
        public static string NewDay(int day, IEnumerable<string> restoredActions)
        {
            if (restoredActions == null) return Strings.Format("day.new", day);

            var available = new List<string>();
            foreach (var action in restoredActions)
                if (!string.IsNullOrEmpty(action)) available.Add(action);

            return available.Count == 0
                ? Strings.Format("day.new", day)
                : Strings.Format(
                    "day.new_with_restored", day, string.Join(", ", available.ToArray()));
        }

        /// <summary>
        /// The one phase transition with real consequences: followers go to sleep, and
        /// night-only content becomes reachable.
        /// </summary>
        public static string Nightfall() => Strings.Get("day.nightfall");

        /// <summary>On-demand readout for the where-am-I key.</summary>
        public static string Status(
            int day, string phase, string nextPhase, float realSecondsRemaining)
        {
            if (string.IsNullOrEmpty(nextPhase) || realSecondsRemaining <= 0f)
                return Strings.Format("day.status", day, phase);

            var seconds = (int)(realSecondsRemaining + 0.5f);
            var duration = Strings.Plural("duration.second", "duration.seconds", seconds);
            return Strings.Format("day.status_with_countdown", day, phase, duration, nextPhase);
        }
    }
}
