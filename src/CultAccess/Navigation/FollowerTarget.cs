using CultAccess.Status;
using CultAccess.Util;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Names and describes a follower from the follower itself rather than from its
    /// interaction label.
    ///
    /// <c>interaction_FollowerInteraction.GetLabel()</c> returns an empty string unless
    /// <c>Interactable</c> is set, and again whenever
    /// <c>PlayerFarming.Location != FollowerLocation.Base</c>. At scan range that leaves the
    /// label blank, so followers used to arrive in the target list as an unnamed, unavailable
    /// entry — findable only by walking into them, which is precisely what the player cannot
    /// do without first finding them.
    ///
    /// The concrete component holds a live <c>follower</c> reference the whole time, so
    /// everything the label would have said is available regardless. Same shape as the weapon
    /// podium fix: read the state, not the label.
    /// </summary>
    internal static class FollowerTarget
    {
        /// <summary>
        /// The stable identity, used as the point of interest's name. Deliberately just the
        /// follower's own name: this is what guidance and arrival announcements repeat, and
        /// "Reached Sinterklaas" is what the player wants there rather than a full dossier.
        /// </summary>
        public static string Name(interaction_FollowerInteraction interaction)
        {
            if (interaction == null) return null;

            try
            {
                // The game's own label wraps this name in colour and trait sprite markup,
                // so clean it even though the source field is normally plain.
                var name = RichText.Clean(interaction.follower?.Brain?.Info?.Name);
                return name.Length == 0 ? null : name;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not read follower name: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// The list entry: who they are, what is wrong with them, and what is worth walking
        /// over for. Computed live on every announcement rather than baked into the scan,
        /// because all three change without the catalogue being rebuilt — a follower falls
        /// ill, finishes a quest, or starts walking towards you between one press and the next.
        ///
        /// Low verbosity keeps the previous behaviour exactly, because at that setting the
        /// player has asked for labels and nothing else.
        /// </summary>
        public static string Describe(
            interaction_FollowerInteraction interaction, string fallbackName, bool detailed)
        {
            var info = interaction?.follower?.Brain?._directInfoAccess;
            if (info == null) return fallbackName;

            try
            {
                var name = RichText.Clean(info.Name);
                if (name.Length == 0) name = fallbackName;

                // Low verbosity is left exactly as it was: the bare name and the kind noun.
                // The player has asked for labels only, and the enriched form is not a label.
                if (!detailed) return $"{name}, follower";

                var identity = FollowerStatusText.Identity(
                    name, FollowerReader.Species(info), info.XPLevel);

                return FollowerStatusText.TargetEntry(
                    identity,
                    FollowerReader.ConditionName(info),
                    FollowerReader.Headline(info, interaction.follower.Brain),
                    detailed: true);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not describe a follower target: {e.Message}");
                return fallbackName;
            }
        }
    }
}
