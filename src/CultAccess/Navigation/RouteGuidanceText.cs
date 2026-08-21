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
            bool isFirstInstruction)
        {
            var movement = isTurn
                ? $"Turn {bearing}. Continue for {stepDistance}"
                : $"{(isFirstInstruction ? "Go" : "Continue")} {bearing} for {stepDistance}";

            return finalStep
                ? $"{movement} to {targetName}."
                : $"{movement}. {targetName}, {remainingDistance} remaining.";
        }

        public static string DirectLine(
            string bearing,
            string targetName,
            string remainingDistance) =>
            $"Continue {bearing} for {remainingDistance} to {targetName}, direct line.";
    }
}
