using CultAccess.Speech;
using UnityEngine;

namespace CultAccess.Diagnostics
{
    /// <summary>
    /// Lets the player stamp the log at the moment something goes wrong.
    ///
    /// The hardest bug reports in this project are the silent ones: an animation plays, or
    /// something is clearly gained, and the mod says nothing. By the time the session ends
    /// there is no way to find that moment in a six-thousand-line log, and reconstructing it
    /// from memory has already failed once.
    ///
    /// One keypress writes a numbered marker plus the live context that is cheap to capture
    /// and usually decisive: where the player was, what location the game thinks they are in,
    /// and what the game is currently asking for.
    /// </summary>
    internal static class LogMarker
    {
        private static int _count;

        internal static void Mark()
        {
            _count++;

            var position = "unknown";
            var location = "unknown";
            try
            {
                var player = Navigation.NavigatorPlayer.Resolve();
                if (player != null)
                {
                    var p = player.position;
                    position = $"({p.x:0.00},{p.y:0.00},{p.z:0.00})";
                }

                location = PlayerFarming.Location.ToString();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not capture marker context: {e.Message}");
            }

            var step = Status.OnboardingTracker.CurrentStep();
            Plugin.Log.LogInfo(
                $"[mark] #{_count} time={Time.unscaledTime:0.00} player={position} " +
                $"location={location} onboardingStep=\"{step}\" " +
                $"storyGateClosed={Navigation.StoryLabelGate.Closed}");

            // Marker 1 of the 2026-08-26 session was stamped because several follower
            // animations were playing nearby and the player wanted to know what they were.
            // The log could not answer: follower tasks are not recorded ambiently, and adding
            // a continuous feed for them would be the noisiest possible instrument. Stamping
            // them here instead makes the marker answer its own question — you press it
            // *because* something is happening, so that is exactly the moment worth capturing.
            LogNearbyFollowers();

            // Spoken so the player knows the stamp registered, and numbered so a verbal note
            // like "marker three was the animation" lines up with the log.
            Speaker.Say($"Log marker {_count}", SpeechPriority.Now);
        }

        internal static void Reset() => _count = 0;

        /// <summary>
        /// Every follower within the scan radius, with what the game says they are doing.
        ///
        /// The task type is the identity of the animation: there are 137 of them and the body
        /// animation *is* the task, so a follower mid-vomit, mid-dance or stuck in poo is
        /// distinguishable here without reading a single Spine track. The cursed state and the
        /// face's own inputs come along because they decide the expression.
        /// </summary>
        private static void LogNearbyFollowers()
        {
            try
            {
                var followers = Follower.Followers;
                var player = PlayerFarming.Instance;
                if (followers == null || player == null)
                {
                    Plugin.Log.LogInfo($"[mark] #{_count} followers=unavailable");
                    return;
                }

                var origin = player.transform.position;
                var radius = Plugin.ScanRadius.Value;
                var reported = 0;

                foreach (var follower in followers)
                {
                    if (follower == null || !follower.isActiveAndEnabled) continue;

                    var brain = follower.Brain;
                    var info = brain?._directInfoAccess;
                    if (info == null) continue;

                    var distance = Vector2.Distance(follower.transform.position, origin);
                    if (distance > radius) continue;

                    reported++;
                    Plugin.Log.LogInfo(
                        $"[mark] #{_count} follower=\"{Util.RichText.Clean(info.Name)}\" " +
                        $"distance={distance:0.0} task={brain.CurrentTaskType} " +
                        $"state={(brain.CurrentState == null ? "none" : brain.CurrentState.Type.ToString())} " +
                        $"cursed={info.CursedState} role={info.FollowerRole} " +
                        $"drunk={info.IsDrunk} rest={info.Rest:0} illness={info.Illness:0} " +
                        $"injured={info.Injured:0}");
                }

                if (reported == 0)
                    Plugin.Log.LogInfo($"[mark] #{_count} followers=none within {radius:0} units");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not capture nearby followers: {e.Message}");
            }
        }
    }
}
