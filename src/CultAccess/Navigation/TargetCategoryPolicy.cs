namespace CultAccess.Navigation
{
    internal enum TargetCategory
    {
        Everything,
        TravelAndExits,
        ActionsNow,
        Facilities,
        Resources,
        StoryAndQuests,
        Enemies,
        Followers,
        Characters,
    }

    /// <summary>
    /// Order, speech names, and ordinary membership rules for target filters.
    ///
    /// There was once an <c>AllTargets</c> entry as well as <c>Everything</c>. The two really
    /// did differ — Everything collapses repeated bulk resource nodes to the nearest useful
    /// one, and AllTargets listed every one of them — but they were sat next to each other in
    /// a cycle under names that are synonyms in English, so the difference was unguessable.
    ///
    /// It was dropped rather than renamed because nothing needed it. Resources already lists
    /// every individual node exhaustively, and Everything already includes anything alive
    /// whether or not a category claims it, so the escape hatch it looked like was one
    /// Everything was already providing. What remained unique to it was a mixed list with
    /// every duplicate resource node in it, which is precisely the unusable list that the
    /// collapsing exists to prevent.
    /// </summary>
    internal static class TargetCategoryPolicy
    {
        private static readonly TargetCategory[] Categories =
        {
            TargetCategory.Everything,
            TargetCategory.TravelAndExits,
            TargetCategory.ActionsNow,
            TargetCategory.Facilities,
            TargetCategory.Resources,
            TargetCategory.StoryAndQuests,
            TargetCategory.Enemies,
            TargetCategory.Followers,
            TargetCategory.Characters,
        };

        public static int Count => Categories.Length;

        public static TargetCategory At(int index) => Categories[index];

        public static bool Matches(TargetCategory category, PointOfInterest poi)
        {
            switch (category)
            {
                case TargetCategory.ActionsNow:
                    return (poi.Interaction != null || poi.IsPendingRecruit) &&
                           !poi.IsAutomaticPassage &&
                           poi.Availability == PoiAvailability.Available;
                case TargetCategory.Facilities:
                    return poi.InteractionRole == InteractionRole.Facility;
                case TargetCategory.Resources:
                    return poi.InteractionRole == InteractionRole.Resource;
                case TargetCategory.StoryAndQuests:
                    return poi.InteractionRole == InteractionRole.Story;
                case TargetCategory.Enemies:
                    return poi.Kind == PoiKind.Enemy;
                case TargetCategory.Followers:
                    return poi.Kind == PoiKind.Follower;
                case TargetCategory.Characters:
                    return poi.Kind == PoiKind.Character;
                default:
                    return false;
            }
        }

        public static string Name(TargetCategory category)
        {
            switch (category)
            {
                case TargetCategory.TravelAndExits: return "travel and exits";
                case TargetCategory.ActionsNow: return "actions now";
                case TargetCategory.Facilities: return "facilities";
                case TargetCategory.Resources: return "resources";
                case TargetCategory.StoryAndQuests: return "story and quests";
                case TargetCategory.Enemies: return "enemies";
                case TargetCategory.Followers: return "followers";
                case TargetCategory.Characters: return "characters";
                default: return "everything";
            }
        }
    }
}
