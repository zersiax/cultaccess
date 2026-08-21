using CultAccess.Speech;
using UnityEngine;

namespace CultAccess.Status
{
    /// <summary>
    /// Reports progress when a dissenting follower is re-educated.
    ///
    /// Re-education is a seven-day course delivered entirely through an invisible number.
    /// Each session subtracts 7.5 from <c>Stats.Reeducation</c>, which starts at 50 when a
    /// follower turns dissenter, and the follower is cured only when it reaches zero — but
    /// nothing said so. A live session shows what that costs: seven re-educations were
    /// performed, one on each of seven followers, leaving every one of them at 42.5 and none
    /// of them cured. The effort was real and the result invisible, so it read as broken.
    /// Concentrating those same seven on one follower would have cured them.
    ///
    /// Driven by <c>FollowerBrainStats.OnReeducationChanged</c>, which is a static event, so
    /// nothing here polls.
    /// </summary>
    internal static class ReeducationAnnouncer
    {
        /// <summary>Removed per session, from <c>interaction_FollowerInteraction</c>.</summary>
        private const float PerSession = 7.5f;

        /// <summary>
        /// Only a drop this large is a re-education. The same value climbs on its own while a
        /// follower dissents, in small increments every brain tick, and narrating that would
        /// be a running commentary on a number the player cannot act on. The rise already has
        /// its own voice: the game's "leaving tomorrow" notification, which the mod speaks.
        /// </summary>
        private const float MeaningfulDrop = 1f;

        private static bool _subscribed;

        internal static void Initialize()
        {
            if (_subscribed) return;

            try
            {
                FollowerBrainStats.OnReeducationChanged += HandleChanged;
                _subscribed = true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not subscribe to re-education changes: {e.Message}");
            }
        }

        private static void HandleChanged(
            int followerID, float newValue, float oldValue, float change)
        {
            if (!Plugin.SpeechEnabled.Value || !Plugin.AnnounceReeducation.Value) return;

            // change is new minus old, so a re-education is negative.
            if (change > -MeaningfulDrop) return;

            var name = NameFor(followerID);
            string spoken;

            if (newValue <= 0f)
            {
                spoken = $"{name} is no longer dissenting.";
            }
            else
            {
                // Ceiling, not rounding: at 5 remaining one more session finishes it, and
                // saying "0 more" while the follower is still dissenting would be a lie.
                var sessions = Mathf.CeilToInt(newValue / PerSession);
                spoken = $"{name} re-educated. Dissent {Round(newValue)} of 100, " +
                         (sessions == 1
                             ? "one more session"
                             : $"{sessions} more sessions, one a day");
            }

            Plugin.Log.LogInfo(
                $"[reeducation] follower={name} id={followerID} " +
                $"before={Round(oldValue)} after={Round(newValue)} spoken=\"{spoken}\"");
            Speaker.Say(spoken, SpeechPriority.Queued);
        }

        /// <summary>
        /// A follower off-screen is a <c>SimFollower</c> rather than a <c>Follower</c>, and
        /// re-education can complete for one that has since walked out of the room, so both
        /// registries are consulted before falling back to the bare ID.
        /// </summary>
        private static string NameFor(int followerID)
        {
            try
            {
                var follower = FollowerManager.FindFollowerByID(followerID);
                var info = follower?.Brain?.Info
                           ?? FollowerManager.FindSimFollowerByID(followerID)?.Brain?.Info;
                if (info != null && !string.IsNullOrEmpty(info.Name)) return info.Name;
            }
            catch (System.Exception)
            {
                // Fall through to the ID rather than losing the announcement entirely.
            }

            return $"Follower {followerID}";
        }

        /// <summary>The meter moves in halves, so one decimal place and no more.</summary>
        private static string Round(float value) => value.ToString("0.#");
    }
}
