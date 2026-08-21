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

            // Spoken so the player knows the stamp registered, and numbered so a verbal note
            // like "marker three was the animation" lines up with the log.
            Speaker.Say($"Log marker {_count}", SpeechPriority.Now);
        }

        internal static void Reset() => _count = 0;
    }
}
