using CultAccess.Localization;
using CultAccess.Util;

namespace CultAccess.Status
{
    /// <summary>
    /// Turns the game's onboarding phase into the player's next step.
    ///
    /// Pure string logic on purpose: the phase arrives as its enum name so this can be
    /// tested without loading the game. Vocabulary follows the game's own terms —
    /// indoctrinate, Shrine, Devotion — so the player can follow guides and dialogue.
    /// </summary>
    internal static class OnboardingText
    {
        /// <summary>
        /// Empty when the game is not asking for anything, which is the common case once
        /// onboarding is finished. Callers omit empty results rather than speaking them.
        /// </summary>
        public static string NextStep(string phase)
        {
            return NextStep(phase, hasPendingRecruit: false);
        }

        /// <summary>
        /// The Shrine phase spans both construction and the new recruit Ratau grants when
        /// construction completes. The phase itself does not change between those beats, so
        /// the save-backed pending-recruit list disambiguates the actual next action.
        /// </summary>
        public static string NextStep(string phase, bool hasPendingRecruit)
        {
            if (string.IsNullOrEmpty(phase)) return string.Empty;

            if (phase == "Shrine" && hasPendingRecruit)
                return Strings.Get("onboarding.indoctrinate_at_platform");

            switch (phase)
            {
                case "Off":
                case "Done":
                    return string.Empty;
                case "Indoctrinate":
                case "IndoctrinateBerriesAllowed":
                    return Strings.Get("onboarding.indoctrinate");
                case "Shrine":
                    return Strings.Get("onboarding.shrine");
                case "Devotion":
                    return Strings.Get("onboarding.devotion");
                default:
                    // A game update can add a phase. Saying its humanized name is far better
                    // than going silent about what the game is waiting for.
                    var humanised = RichText.Humanise(phase);
                    return humanised.Length == 0
                        ? string.Empty
                        : Strings.Format("onboarding.unknown", humanised.ToLowerInvariant());
            }
        }
    }
}
