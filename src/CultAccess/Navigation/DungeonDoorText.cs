using CultAccess.Localization;

namespace CultAccess.Navigation
{
    internal static class DungeonDoorText
    {
        /// <summary>
        /// The threshold before the owned count. The game's own label puts them the other way
        /// round, which read as backwards in speech; a pure test locks this order.
        /// </summary>
        public static string Requirement(int currentFollowers, int requiredFollowers)
        {
            var key = requiredFollowers == 1
                ? "dungeon_door.requirement_one"
                : "dungeon_door.requirement_many";
            return Strings.Format(key, requiredFollowers, currentFollowers);
        }
    }
}
