namespace CultAccess.UI
{
    /// <summary>Pure wording policy shared by progression-menu readers.</summary>
    public static class ProgressionText
    {
        public static string Position(string label, int zeroBasedIndex, int count) =>
            $"{label} {zeroBasedIndex + 1} of {count}";

        public static string Cost(string itemName, int required, int owned) =>
            $"cost {required} {itemName}, {owned} owned";

        public static string ChooseThenConfirm(
            string confirmKey, bool holdActions, string action) =>
            holdActions
                ? $"press {confirmKey} to choose, then keep {confirmKey} held for 3 seconds to {action}"
                : $"press {confirmKey} to choose, then press {confirmKey} again to {action}";

        public static string Confirm(string confirmKey, bool holdActions, string action) =>
            holdActions
                ? $"hold {confirmKey} for 3 seconds to {action}"
                : $"press {confirmKey} to {action}";
    }
}
