using CultAccess.Util;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Names a follower from the follower itself rather than from its interaction label.
    ///
    /// <c>interaction_FollowerInteraction.GetLabel()</c> returns an empty string unless
    /// <c>Interactable</c> is set, and again whenever
    /// <c>PlayerFarming.Location != FollowerLocation.Base</c>. At scan range that leaves the
    /// label blank, so followers used to arrive in the target list as an unnamed, unavailable
    /// entry — findable only by walking into them, which is precisely what the player cannot
    /// do without first finding them.
    ///
    /// The concrete component holds a live <c>follower</c> reference the whole time, so the
    /// real name is always available. Same shape as the weapon podium fix: read the state,
    /// not the label.
    /// </summary>
    internal static class FollowerTarget
    {
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
    }
}
