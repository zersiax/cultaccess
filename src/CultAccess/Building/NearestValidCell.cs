using System;
using CultAccess.Speech;
using HarmonyLib;
using UnityEngine;

namespace CultAccess.Building
{
    /// <summary>
    /// Finds the closest grid cell where the current structure would actually fit, and says
    /// which way it lies.
    ///
    /// A live session found sixteen cells refused in a row while placing a single sleeping
    /// bag. The readout could say a cell was no good but never where a good one was, so the
    /// only strategy was to walk the cursor outward and listen. Naming the reason helps, but
    /// it still leaves the player searching; this answers the question directly.
    ///
    /// **Reports rather than moves.** Driving the cursor would mean synthesising the game's
    /// own placement input, which is fragile, and it would take a decision out of the
    /// player's hands — rules section 13 asks for information rather than automation. A
    /// bearing and a distance is what a sighted player gets from looking at the grid, and it
    /// leaves them to walk it.
    ///
    /// Validity is asked of the game's own <c>IsValidPlacement</c> through
    /// <see cref="PlacementDescriber.IsValid"/>, so this search and the spoken readout can
    /// never disagree about whether a cell is good.
    /// </summary>
    internal static class NearestValidCell
    {
        /// <summary>Same private cursor field the placement announcer reads.</summary>
        private static readonly AccessTools.FieldRef<PlacementRegion, PlacementTile> CurrentTileRef =
            AccessTools.FieldRefAccess<PlacementRegion, PlacementTile>("CurrentTile");

        /// <summary>
        /// How far out to look, in cells. The base grid is bounded and a ring search is
        /// cheap, but this runs on a keypress inside a frame, so it stops at a radius well
        /// past anything a player would want to walk to rather than sweeping the whole grid.
        /// </summary>
        private const int MaxRadius = 24;

        internal static void Announce()
        {
            var region = PlacementRegion.Instance;
            if (region == null || !region.gameObject.activeInHierarchy)
            {
                Speaker.Say("Not placing anything.");
                return;
            }

            var current = SafeCurrentTile(region);
            if (current == null)
            {
                Speaker.Say("No placement cell selected.");
                return;
            }

            var origin = current.GridPosition;
            if (PlacementDescriber.IsValid(region, current.Position))
            {
                Speaker.Say("This cell is already valid.");
                return;
            }

            if (!TryFind(region, origin, out var found, out var distance))
            {
                Plugin.Log.LogInfo(
                    $"[nearest valid cell] none within {MaxRadius} of ({origin.x},{origin.y})");
                Speaker.Say($"No valid cell within {MaxRadius} squares.");
                return;
            }

            var spoken = $"Nearest valid cell, {Bearing(found - origin)}, " +
                         $"{distance} {(distance == 1 ? "square" : "squares")} away, " +
                         $"grid x {found.x}, y {found.y}";
            Plugin.Log.LogInfo(
                $"[nearest valid cell] from=({origin.x},{origin.y}) to=({found.x},{found.y}) " +
                $"distance={distance} spoken=\"{spoken}\"");
            Speaker.Say(spoken);
        }

        private static PlacementTile SafeCurrentTile(PlacementRegion region)
        {
            try { return CurrentTileRef(region); }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// Rings outward from the cursor, nearest first, so the first hit is the closest.
        /// Distance is Chebyshev — the ring index — because the cursor moves a cell at a time
        /// in eight directions, so that is the number of presses it will actually take.
        /// </summary>
        private static bool TryFind(
            PlacementRegion region, Vector2Int origin, out Vector2Int found, out int distance)
        {
            found = origin;
            distance = 0;

            for (var radius = 1; radius <= MaxRadius; radius++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    for (var dy = -radius; dy <= radius; dy++)
                    {
                        // Only the ring's edge is new; the interior was covered by a smaller
                        // radius and re-testing it would report a farther cell as nearer.
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;

                        var candidate = new Vector2Int(origin.x + dx, origin.y + dy);
                        var cell = region.GetTileGridTile(candidate);
                        if (cell == null) continue;

                        // IsValidPlacement takes grid coordinates in a Vector3 and casts them
                        // to int; it is not a world position, and passing one would test a
                        // cell somewhere else entirely.
                        if (!PlacementDescriber.IsValid(
                                region, new Vector3(candidate.x, candidate.y, 0f)))
                            continue;

                        found = candidate;
                        distance = radius;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Compass wording matching the placement readout's own, so "2 north 1 east" from the
        /// refusal reason and "north east" from here describe the same grid the same way.
        /// </summary>
        private static string Bearing(Vector2Int delta)
        {
            var vertical = delta.y > 0 ? "north" : delta.y < 0 ? "south" : null;
            var horizontal = delta.x > 0 ? "east" : delta.x < 0 ? "west" : null;

            if (vertical == null) return horizontal ?? "here";
            return horizontal == null ? vertical : $"{vertical} {horizontal}";
        }
    }
}
