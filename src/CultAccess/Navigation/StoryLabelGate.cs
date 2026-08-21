using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Detects the game's global story suppression of interaction labels.
    ///
    /// <c>Interaction.Label</c> returns the empty string for every interaction while
    /// <c>!IgnoreTutorial &amp;&amp; !DataManager.Instance.AllowBuilding &amp;&amp;
    /// BiomeBaseManager.Instance != null</c>. <c>AllowBuilding</c> is a story flag that
    /// <c>Onboarding</c> toggles, not a distance or proximity check, so when it closes the
    /// entire world goes label-less at once. <c>Interactor</c> then refuses to select anything
    /// without a label, so nothing in the world can actually be used until the beat ends.
    ///
    /// This matters because the resulting silence is indistinguishable from a broken mod. A
    /// scan reported sixty-five targets and zero usable actions, which read as a progression
    /// softlock. The honest fix is not to fake availability — the player really cannot press
    /// Interact on anything — but to say why, and to keep saying what the game is waiting for.
    ///
    /// Evaluated once per scan rather than per interaction: <c>BiomeBaseManager.Instance</c>
    /// is a singleton lookup and the condition is global by construction.
    /// </summary>
    internal static class StoryLabelGate
    {
        private static bool _closed;
        private static int _evaluatedFrame = -1;

        /// <summary>
        /// True while the game is blanking interaction labels for a story beat. Cached per
        /// frame so a scan that inspects dozens of interactions pays for one lookup.
        /// </summary>
        public static bool Closed
        {
            get
            {
                if (_evaluatedFrame == Time.frameCount) return _closed;
                _evaluatedFrame = Time.frameCount;
                _closed = Evaluate();
                return _closed;
            }
        }

        /// <summary>
        /// True when this specific interaction's blank label is explained by the story gate.
        /// Interactions that set <c>IgnoreTutorial</c> keep their labels throughout and are
        /// therefore never suppressed by it.
        /// </summary>
        public static bool Suppresses(Interaction interaction)
        {
            if (interaction == null) return false;

            try
            {
                return !interaction.IgnoreTutorial && Closed;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not read IgnoreTutorial: {e.Message}");
                return false;
            }
        }

        private static bool Evaluate()
        {
            try
            {
                var data = DataManager.Instance;
                if (data == null || data.AllowBuilding) return false;

                // The third clause of the game's own condition. Outside a biome base the
                // primary label is not suppressed, even with building disallowed.
                return BiomeBaseManager.Instance != null;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not evaluate the story label gate: {e.Message}");
                return false;
            }
        }
    }
}
