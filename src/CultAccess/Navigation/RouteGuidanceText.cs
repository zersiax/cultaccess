using CultAccess.Localization;

namespace CultAccess.Navigation
{
    /// <summary>Pure wording for route instructions, kept testable without Unity.</summary>
    internal static class RouteGuidanceText
    {
        public static string Step(
            string bearing,
            string stepDistance,
            string targetName,
            string remainingDistance,
            bool finalStep,
            bool isTurn,
            bool isFirstInstruction,
            bool blockedAhead = false)
        {
            var movement = isTurn
                ? $"Turn {bearing}. Continue for {stepDistance}"
                : $"{(isFirstInstruction ? "Go" : "Continue")} {bearing} for {stepDistance}";

            var instruction = finalStep
                ? $"{movement} to {targetName}."
                : $"{movement}. {targetName}, {remainingDistance} remaining.";

            return WithBlocked(instruction, blockedAhead);
        }

        public static string DirectLine(
            string bearing,
            string targetName,
            string remainingDistance,
            bool blockedAhead = false) =>
            WithBlocked(
                $"Continue {bearing} for {remainingDistance} to {targetName}, direct line.",
                blockedAhead);

        /// <summary>
        /// Says so when the way the instruction points is the way the wall sonar is already
        /// reporting contact.
        ///
        /// Measured 2026-08-25: the mod said "Continue north east for 1.6 metres" while the
        /// Lamb was flat against a collider to the north east and the blocked cue was playing.
        /// Two subsystems describing the same moment and contradicting each other, with the
        /// words winning because they are the more specific-sounding of the two. The cue says
        /// something is there; only the words can say it is in the way of what you were just
        /// told to do.
        /// </summary>
        private static string WithBlocked(string instruction, bool blockedAhead) =>
            blockedAhead ? $"{instruction} {Strings.Get("route.blocked")}" : instruction;
    }
}
