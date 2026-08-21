using System.Reflection;
using CultAccess.Speech;
using HarmonyLib;
using MMTools;
using UnityEngine;
using UnityEngine.Video;

namespace CultAccess.Patches
{
    /// <summary>
    /// Breaks the intro-video deadlock.
    ///
    /// <c>MMVideoPlayer.Update</c> has two exits and a stalled video reaches neither:
    ///
    /// <list type="bullet">
    /// <item>The completion check requires <c>videoPlayer.frame &gt; 0</c>, so a video that
    /// never renders its first frame is never considered finished.</item>
    /// <item>The skip handler is nested *inside* <c>if (videoPlayer.isPlaying)</c>, so when
    /// playback never starts the skip input is not even read — the "Skip" prompt is drawn
    /// by <c>Play</c> regardless, so the key appears dead.</item>
    /// </list>
    ///
    /// <c>errorReceived</c> only fires for errors the player reports; a null clip from a
    /// failed <c>Resources.Load</c> is silent. Result: a black screen, a Skip prompt that
    /// does nothing, and no way forward. Unrecoverable without sight, and not much better
    /// with it.
    ///
    /// We watch for a video that is not progressing and drive the game's own
    /// <c>EndReached</c>, which runs the normal completion path including the callback that
    /// advances startup.
    /// </summary>
    [HarmonyPatch]
    internal static class VideoWatchdog
    {
        private static readonly MethodInfo EndReachedMethod =
            AccessTools.Method(typeof(MMVideoPlayer), "EndReached", new[] { typeof(VideoPlayer) });

        private static readonly AccessTools.FieldRef<MMVideoPlayer, bool> CompletedRef =
            AccessTools.FieldRefAccess<MMVideoPlayer, bool>("completed");

        /// <summary>When the current stall started, or -1 when progressing.</summary>
        private static float _stallSince = -1f;

        private static VideoPlayer CurrentPlayer =>
            AccessTools.StaticFieldRefAccess<VideoPlayer>(typeof(MMVideoPlayer), "videoPlayer");

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MMVideoPlayer), "Update")]
        private static void WatchForStalledVideo(MMVideoPlayer __instance)
        {
            if (!Plugin.VideoWatchdogEnabled.Value) return;
            if (EndReachedMethod == null) return;

            var player = CurrentPlayer;
            if (player == null || CompletedRef(__instance))
            {
                _stallSince = -1f;
                return;
            }

            // Genuine playback: frames are being produced.
            if (player.isPlaying && player.frame > 0)
            {
                _stallSince = -1f;
                return;
            }

            if (_stallSince < 0f)
            {
                _stallSince = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _stallSince < Plugin.VideoStallTimeout.Value) return;

            _stallSince = -1f;
            Plugin.Log.LogWarning(
                $"Video stalled for {Plugin.VideoStallTimeout.Value}s with no frames rendered " +
                $"(isPlaying={player.isPlaying}, frame={player.frame}, clip={(player.clip == null ? "null" : player.clip.name)}). " +
                "Forcing completion so startup can continue.");

            Speaker.Say("Video did not play. Skipping.", SpeechPriority.Now);
            ForceEnd(player);
        }

        /// <summary>
        /// Optional: end every video the moment it starts. Intro and update videos carry no
        /// accessible content, and this removes them from the startup path entirely.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MMVideoPlayer), nameof(MMVideoPlayer.Play))]
        private static void SkipVideoEntirely()
        {
            if (!Plugin.SkipAllVideos.Value) return;

            var player = CurrentPlayer;
            if (player == null || EndReachedMethod == null) return;

            Plugin.Log.LogInfo("SkipAllVideos is on; ending video immediately.");
            ForceEnd(player);
        }

        private static void ForceEnd(VideoPlayer player)
        {
            try
            {
                EndReachedMethod.Invoke(null, new object[] { player });
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Failed to force video completion: {e}");
                Speaker.Say("Could not skip the video. The game may be stuck.", SpeechPriority.Now);
            }
        }
    }
}
