using System.Collections.Generic;
using CultAccess.Util;
using Lamb.UI;
using UnityEngine.UI;

namespace CultAccess.UI
{
    /// <summary>
    /// Reads the world map's destinations.
    ///
    /// The map is a picture with icons scattered over it and no text beside them, so the
    /// generic reader had nothing to work with and the window announced no destinations at
    /// all — which on a screen whose entire purpose is choosing where to go leaves the player
    /// unable to use it.
    ///
    /// <c>WorldMapIcon</c> carries everything needed: <c>GetLocalisedLocation</c> for the
    /// name in the player's language, and <c>Location</c> for the identity behind it.
    /// <c>DungeonWorldMapIcon</c> adds an <c>IconState</c> saying whether a region is
    /// selectable, already completed, still locked or not yet revealed — which is the
    /// difference between a place you can go and a place you are looking at.
    ///
    /// An unrevealed icon is deliberately still named as unrevealed rather than skipped: it
    /// occupies a position in the map's navigation order, and silence there would read as the
    /// cursor having stopped moving.
    /// </summary>
    internal static class WorldMapDescriber
    {
        internal static bool TryDescribe(Selectable selectable, out string description)
        {
            description = string.Empty;
            if (selectable == null) return false;

            var icon = selectable.GetComponentInParent<WorldMapIcon>();
            if (icon == null) return false;

            var parts = new List<string>(4) { Name(icon) };

            if (SelectableDescriber.Verbosity != Verbosity.Low)
            {
                var state = State(icon);
                if (state != null) parts.Add(state);

                AddPosition(parts, icon);
            }

            if (SelectableDescriber.Verbosity == Verbosity.Diagnostic)
                parts.Add($"[{icon.Location}]");

            description = string.Join(", ", parts.ToArray());
            Plugin.Log.LogInfo(
                $"[world map] location={icon.Location} state={StateName(icon)} " +
                $"spoken=\"{description}\"");
            return true;
        }

        private static string Name(WorldMapIcon icon)
        {
            try
            {
                var localised = icon.GetLocalisedLocation();
                if (RichText.IsUsableLocalization(localised, icon.LocationTerm))
                    return RichText.Clean(localised);
            }
            catch (System.Exception)
            {
                // Fall through rather than losing the entry entirely.
            }

            return RichText.Humanise(icon.Location.ToString());
        }

        /// <summary>
        /// Only dungeon icons carry a state. The plain ones are fixed places on the map and
        /// saying nothing about them is correct, rather than a gap.
        /// </summary>
        private static string State(WorldMapIcon icon)
        {
            var dungeon = icon as DungeonWorldMapIcon;
            if (dungeon == null) return null;

            switch (dungeon.CurrentState)
            {
                case DungeonWorldMapIcon.IconState.Selectable: return "available";
                case DungeonWorldMapIcon.IconState.Completed: return "completed";
                case DungeonWorldMapIcon.IconState.Locked: return "locked";
                case DungeonWorldMapIcon.IconState.Unrevealed: return "not yet revealed";
                case DungeonWorldMapIcon.IconState.Preview: return "preview";
                default: return null;
            }
        }

        private static string StateName(WorldMapIcon icon)
        {
            var dungeon = icon as DungeonWorldMapIcon;
            return dungeon == null ? "<none>" : dungeon.CurrentState.ToString();
        }

        /// <summary>
        /// Position among the map's icons, counted from the live children. The controller
        /// keeps them in a private array, and the children are the same set in the same order
        /// without the reflection.
        /// </summary>
        private static void AddPosition(List<string> parts, WorldMapIcon icon)
        {
            var menu = icon.GetComponentInParent<UIWorldMapMenuController>();
            var icons = menu == null ? null : menu.GetComponentsInChildren<WorldMapIcon>(false);
            if (icons == null) return;

            var index = System.Array.IndexOf(icons, icon);
            if (index >= 0)
                parts.Add(ProgressionText.Position("location", index, icons.Length));
        }
    }
}
