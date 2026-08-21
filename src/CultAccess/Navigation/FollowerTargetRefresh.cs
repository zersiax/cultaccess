using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Keeps the navigation catalogue in step with follower recruitment.
    ///
    /// A gifted or rescued follower appears without any world change the scanner would
    /// notice, so a cached catalogue never learns about them. The same event also clears the
    /// placement region's "Indoctrinate Follower Before Building" block, which otherwise
    /// keeps reporting a requirement the player has already met until they rescan by hand.
    ///
    /// <c>FollowerRecruit</c> publishes exactly the three signals needed; none were used
    /// before. Refreshes are silent: the game's own recruitment notification card and the
    /// onboarding step already say a follower is waiting, and a third utterance for the same
    /// event would be noise.
    /// </summary>
    internal static class FollowerTargetRefresh
    {
        /// <summary>
        /// Recruitment assigns the <c>Follower</c> and the interaction's own follower
        /// reference from coroutines that continue past the event, so the catalogue is
        /// rebuilt after a short settle rather than on the same frame. A sampling choice,
        /// not a value derived from game data.
        /// </summary>
        private const float SettleSeconds = 0.5f;

        private static bool _subscribed;
        private static bool _refreshPending;
        private static int _refreshFrame;
        private static float _refreshAt;
        private static string _reason;

        internal static void Initialize()
        {
            if (_subscribed) return;

            try
            {
                FollowerRecruit.OnNewRecruit += HandleNewRecruit;
                FollowerRecruit.OnFollowerRecruited += HandleRecruited;
                FollowerRecruit.OnRecruitFinalised += HandleFinalised;
                _subscribed = true;
            }
            catch (System.Exception e)
            {
                // One missing event must not cost the player every other follower feature.
                Plugin.Log.LogWarning(
                    $"Could not subscribe to follower recruitment events: {e.Message}");
            }
        }

        internal static void Tick()
        {
            if (!_refreshPending || Time.frameCount < _refreshFrame ||
                Time.unscaledTime < _refreshAt)
                return;

            _refreshPending = false;
            Plugin.Log.LogInfo($"[follower targets] rescan reason={_reason}");
            Navigator.RefreshAfterWorldChange();
        }

        internal static void Shutdown()
        {
            if (_subscribed)
            {
                try
                {
                    FollowerRecruit.OnNewRecruit -= HandleNewRecruit;
                    FollowerRecruit.OnFollowerRecruited -= HandleRecruited;
                    FollowerRecruit.OnRecruitFinalised -= HandleFinalised;
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning(
                        $"Could not detach follower recruitment events: {e.Message}");
                }
                _subscribed = false;
            }

            _refreshPending = false;
            _reason = null;
        }

        private static void HandleNewRecruit() => Schedule("new-recruit");

        private static void HandleRecruited(FollowerInfo info) =>
            Schedule($"recruited-{Describe(info)}");

        private static void HandleFinalised(FollowerInfo info) =>
            Schedule($"finalised-{Describe(info)}");

        private static void Schedule(string reason)
        {
            _refreshPending = true;
            _refreshFrame = Time.frameCount + 2;
            _refreshAt = Time.unscaledTime + SettleSeconds;
            _reason = reason;
        }

        private static string Describe(FollowerInfo info)
        {
            try
            {
                var name = info?.Name;
                return string.IsNullOrEmpty(name) ? "unnamed" : name;
            }
            catch
            {
                return "unnamed";
            }
        }
    }
}
