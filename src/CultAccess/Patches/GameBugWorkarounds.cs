using HarmonyLib;
using Lamb.UI;

namespace CultAccess.Patches
{
    /// <summary>
    /// Workarounds for pre-existing game bugs that get in the way of accessibility work.
    /// These are not accessibility features; they exist because they pollute the log we
    /// depend on as our only view into a running game.
    /// </summary>
    [HarmonyPatch]
    internal static class GameBugWorkarounds
    {
        private static int _suppressedFrames;
        private static bool _reported;

        /// <summary>
        /// <c>SkeletonAnimationLODGlobalManager.Update</c> dereferences
        /// <c>MonoSingleton&lt;UIManager&gt;.Instance</c> with no null check:
        ///
        /// <code>if (MonoSingleton&lt;UIManager&gt;.Instance.IsPaused || ...)</code>
        ///
        /// The Spine LOD manager starts updating before UIManager is instantiated, so
        /// every frame in that window costs a scene-wide <c>FindObjectOfType</c> (from
        /// MonoSingleton's null fallback), a thrown NullReferenceException, a stack trace
        /// written to disk, and a Unity crash-report upload. It resolves itself the moment
        /// UIManager appears, so the game "works" — it just screams for a second or two
        /// and buries anything useful in the log.
        ///
        /// Skipping the update while UIManager is absent is safe: the method's entire body
        /// is LOD quality selection, which has nothing to do to before the UI exists.
        ///
        /// Cheap in steady state — MonoSingleton only pays for FindObjectOfType while
        /// its cached instance is null, so this is a static field read once UI is up.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SkeletonAnimationLODGlobalManager), "Update")]
        private static bool SkipSkeletonLodUpdateBeforeUIManagerExists()
        {
            if (!Plugin.SuppressSkeletonLodSpam.Value) return true;

            if (MonoSingleton<UIManager>.Instance != null)
            {
                if (_suppressedFrames > 0 && !_reported)
                {
                    _reported = true;
                    Plugin.Log.LogInfo(
                        $"Suppressed {_suppressedFrames} frame(s) of SkeletonAnimationLODGlobalManager " +
                        "NullReferenceException spam before UIManager existed (pre-existing game bug).");
                }

                return true;
            }

            _suppressedFrames++;
            return false;
        }
    }
}
