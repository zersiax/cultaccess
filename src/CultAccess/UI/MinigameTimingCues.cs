using System;
using System.Collections.Generic;
using CultAccess.Audio;
using HarmonyLib;

namespace CultAccess.UI
{
    /// <summary>
    /// Emits a low-latency chirp on entry to the game's live cooking/tailoring success
    /// geometry. This is observation only: it never calls an input method or consumes a
    /// tailor target, and it is disabled while the corresponding automatic mode is on.
    /// </summary>
    [HarmonyPatch]
    internal static class MinigameTimingCues
    {
        private static readonly AccessTools.FieldRef<UICookingMinigameOverlayController, bool>
            CookingPausedRef =
                AccessTools.FieldRefAccess<UICookingMinigameOverlayController, bool>(
                    "MiniGamePaused");

        private static readonly AccessTools.FieldRef<UITailorMinigameOverlayController, bool>
            TailorPausedRef =
                AccessTools.FieldRefAccess<UITailorMinigameOverlayController, bool>(
                    "MiniGamePaused");

        /// <summary>
        /// The game's success test uses two private, difficulty-dependent ranges that also
        /// carry a per-round random offset. Reading them is the only way to mirror the exact
        /// gate; approximating it from the SafeZone rect disagreed with the real check.
        /// </summary>
        private static readonly Func<UICookingMinigameOverlayController, float>
            UnderCookedRangeOf = RangeGetter("UnderCookedRange");

        private static readonly Func<UICookingMinigameOverlayController, float>
            OverCookedRangeOf = RangeGetter("OverCookedRange");

        private sealed class CookingPass
        {
            public bool Inside;
            public bool Cued;
        }

        private static readonly Dictionary<UICookingMinigameOverlayController, CookingPass>
            CookingZoneStates =
                new Dictionary<UICookingMinigameOverlayController, CookingPass>();

        private static readonly Dictionary<UITailorMinigameOverlayController, bool>
            TailorZoneStates = new Dictionary<UITailorMinigameOverlayController, bool>();

        private static bool _reportedCookingGeometryFailure;
        private static bool _reportedTailorGeometryFailure;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UICookingMinigameOverlayController), "SetBarSizes")]
        private static void ResetCookingWindow(UICookingMinigameOverlayController __instance)
        {
            if (__instance == null) return;

            CookingZoneStates[__instance] = new CookingPass();
            LogCookingGeometry(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UICookingMinigameOverlayController), "Update")]
        private static void AfterCookingUpdate(UICookingMinigameOverlayController __instance)
        {
            if (__instance == null || !Plugin.SpeechEnabled.Value ||
                SettingsManager.Settings.Accessibility.AutoCook || CookingPausedRef(__instance))
            {
                // A paused or auto-cooked minigame must not leave a tone running.
                TimingTone.Stop();
                if (__instance != null) CookingZoneStates[__instance] = new CookingPass();
                return;
            }

            if (!CookingZoneStates.TryGetValue(__instance, out var pass) || pass == null)
            {
                pass = new CookingPass();
                CookingZoneStates[__instance] = pass;
            }

            if (!TryReadCookingWindow(__instance, out var x, out var left, out var right))
            {
                TimingTone.Stop();
                return;
            }

            var inside = x > left && x < right;

            // The window's duration is the information a single chirp could never carry:
            // nothing is evaluated until the player presses Interact, so they need to know
            // not just that the window opened but that it is still open. Hold a tone for
            // exactly as long as pressing would succeed.
            if (inside && !pass.Inside) TimingTone.Start();
            else if (!inside && pass.Inside) TimingTone.Stop();

            // Leaving the window ends this pass, so the return trip is cued again.
            if (pass.Inside && !inside) pass.Cued = false;
            pass.Inside = inside;

            if (inside || pass.Cued) return;

            // A short chirp ahead of the boundary is the "get ready" half of the vocabulary:
            // distinct from the sustained tone, and far enough ahead to be acted on.
            if (!TryTimeToEntry(__instance, x, left, right, out var seconds)) return;
            if (seconds > Plugin.CookingCueLead.Value) return;

            pass.Cued = true;
            TimingCue.Play();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UICookingMinigameOverlayController), "OnDisable")]
        private static void ForgetCookingWindow(UICookingMinigameOverlayController __instance)
        {
            TimingTone.Stop();
            if (__instance != null) CookingZoneStates.Remove(__instance);
        }

        internal static void Shutdown()
        {
            TimingTone.Shutdown();
            CookingZoneStates.Clear();
            TailorZoneStates.Clear();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UITailorMinigameOverlayController), "SetBarSizes")]
        private static void ResetTailorWindow(UITailorMinigameOverlayController __instance)
        {
            if (__instance != null) TailorZoneStates[__instance] = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UITailorMinigameOverlayController), "Update")]
        private static void AfterTailorUpdate(UITailorMinigameOverlayController __instance)
        {
            if (__instance == null || !Plugin.SpeechEnabled.Value ||
                SettingsManager.Settings.Accessibility.AutoCraft || TailorPausedRef(__instance))
            {
                if (__instance != null) TailorZoneStates[__instance] = false;
                return;
            }

            var inside = IsTailorTrackerInTarget(__instance);
            TailorZoneStates.TryGetValue(__instance, out var wasInside);
            TailorZoneStates[__instance] = inside;
            if (inside && !wasInside) TimingCue.Play();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UITailorMinigameOverlayController), "OnDisable")]
        private static void ForgetTailorWindow(UITailorMinigameOverlayController __instance)
        {
            if (__instance != null) TailorZoneStates.Remove(__instance);
        }

        /// <summary>
        /// Mirrors the game's own success test exactly: it compares the tracker's anchored X
        /// against GameWidth inset by the two difficulty ranges. Reading the SafeZone rect
        /// through a world-space inverse transform instead was a parallel model, and it
        /// disagreed with the check that actually decides the meal.
        /// </summary>
        private static bool TryReadCookingWindow(
            UICookingMinigameOverlayController controller,
            out float x,
            out float left,
            out float right)
        {
            x = left = right = 0f;

            try
            {
                if (controller.Tracker == null ||
                    UnderCookedRangeOf == null || OverCookedRangeOf == null)
                    return false;

                var width = controller.GameWidth;
                x = controller.Tracker.rectTransform.anchoredPosition.x;
                left = -width / 2f + UnderCookedRangeOf(controller);
                right = width / 2f - OverCookedRangeOf(controller);
                return right > left;
            }
            catch (Exception e)
            {
                if (!_reportedCookingGeometryFailure)
                {
                    _reportedCookingGeometryFailure = true;
                    Plugin.Log.LogWarning($"Could not read cooking success window: {e.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// Seconds until the tracker crosses into the window. The tracker ping-pongs across
        /// GameWidth at a per-recipe Speed, so travel is GameWidth * Speed pixels per second
        /// and the direction comes from which half of the ping-pong cycle is current.
        /// False when it is not currently heading for the window.
        /// </summary>
        private static bool TryTimeToEntry(
            UICookingMinigameOverlayController controller,
            float x,
            float left,
            float right,
            out float seconds)
        {
            seconds = 0f;

            var pixelsPerSecond = controller.GameWidth * controller.Speed;
            if (pixelsPerSecond <= 0f) return false;

            var movingRight =
                UnityEngine.Mathf.FloorToInt(controller.TrackerNormalisedPosition) % 2 == 0;

            float distance;
            if (movingRight)
            {
                if (x >= left) return false;
                distance = left - x;
            }
            else
            {
                if (x <= right) return false;
                distance = x - right;
            }

            seconds = distance / pixelsPerSecond;
            return true;
        }

        private static Func<UICookingMinigameOverlayController, float> RangeGetter(string name)
        {
            try
            {
                var getter = AccessTools.PropertyGetter(
                    typeof(UICookingMinigameOverlayController), name);
                if (getter == null)
                {
                    Plugin.Log.LogWarning($"Cooking range property {name} not found.");
                    return null;
                }

                return AccessTools.MethodDelegate<
                    Func<UICookingMinigameOverlayController, float>>(getter);
            }
            catch (Exception e)
            {
                // Without these the cue simply stays silent rather than cueing a wrong moment.
                Plugin.Log.LogWarning($"Could not bind cooking range {name}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Records the live geometry once per round. The window width and dwell time are what
        /// determine whether a reaction lead is even achievable for a given recipe, and they
        /// change with difficulty plus a per-round random offset.
        /// </summary>
        private static void LogCookingGeometry(UICookingMinigameOverlayController controller)
        {
            if (!TryReadCookingWindow(controller, out _, out var left, out var right)) return;

            var pixelsPerSecond = controller.GameWidth * controller.Speed;
            var dwell = pixelsPerSecond > 0f ? (right - left) / pixelsPerSecond : 0f;
            Plugin.Log.LogInfo(
                $"[cooking window] gameWidth={controller.GameWidth:0.##} " +
                $"speed={controller.Speed:0.###} left={left:0.##} right={right:0.##} " +
                $"windowPx={(right - left):0.##} dwellSeconds={dwell:0.###} " +
                $"leadSeconds={Plugin.CookingCueLead.Value:0.###}");
        }

        /// <summary>
        /// Non-mutating mirror of the game's IsTargetHit geometry. Calling that method
        /// merely to observe it would consume the target and automate the challenge.
        /// </summary>
        private static bool IsTailorTrackerInTarget(
            UITailorMinigameOverlayController controller)
        {
            try
            {
                if (controller.targets == null) return false;
                var trackerAngle = 360f * controller.NormalisedPosition;

                foreach (var target in controller.targets)
                {
                    if (target?.image == null || !target.image.gameObject.activeSelf) continue;

                    var end = target.image.transform.rotation.eulerAngles.z;
                    var start = Utils.Repeat(
                        end - target.image.fillAmount * 360f, 360f);

                    if (start > end)
                    {
                        if (trackerAngle < end || trackerAngle > start) return true;
                    }
                    else if (trackerAngle > start && trackerAngle < end)
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                if (!_reportedTailorGeometryFailure)
                {
                    _reportedTailorGeometryFailure = true;
                    Plugin.Log.LogWarning($"Could not read tailor timing targets: {e.Message}");
                }
            }

            return false;
        }
    }
}
