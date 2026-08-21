using CultAccess.Localization;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Wording for enterable buildings. Pure so it can be tested without the game.
    ///
    /// These are walk-in doorways, not Interact prompts, so every sentence has to say so:
    /// a player who presses E at a Temple door gets nothing and reasonably concludes the
    /// target is broken.
    /// </summary>
    internal static class BuildingEntranceText
    {
        public static string Describe(string name, bool open) =>
            Strings.Format(open ? "entrance.open" : "entrance.blocked", name);

        public static string Arrival(string name, bool open) =>
            Strings.Format(open ? "entrance.arrival_open" : "entrance.arrival_blocked", name);
    }
}
