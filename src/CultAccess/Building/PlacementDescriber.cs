using System;
using System.Collections.Generic;
using System.Reflection;
using CultAccess.Util;
using HarmonyLib;
using UnityEngine;

namespace CultAccess.Building
{
    /// <summary>Formats one live placement cell from the game's own grid and confirm gates.</summary>
    internal static class PlacementDescriber
    {
        private static readonly AccessTools.FieldRef<PlacementRegion, bool> IsPathRef =
            AccessTools.FieldRefAccess<PlacementRegion, bool>("isPath");
        private static readonly AccessTools.FieldRef<PlacementRegion, bool> OnlyRanchRef =
            AccessTools.FieldRefAccess<PlacementRegion, bool>("structureIsOnlyPlaceableInRanch");
        private static readonly AccessTools.FieldRef<PlacementRegion, List<Vector2Int>> RanchTilesRef =
            AccessTools.FieldRefAccess<PlacementRegion, List<Vector2Int>>("ranchingGridPositions");
        private static readonly AccessTools.FieldRef<PlacementRegion, List<Vector2Int>> FenceTilesRef =
            AccessTools.FieldRefAccess<PlacementRegion, List<Vector2Int>>("fenceableGridPositions");
        private static readonly MethodInfo IsValidPlacementMethod =
            AccessTools.Method(typeof(PlacementRegion), "IsValidPlacement");
        private static readonly AccessTools.FieldRef<PlacementRegion, PlacementObject> PlacementObjectRef =
            AccessTools.FieldRefAccess<PlacementRegion, PlacementObject>("placementObject");
        private static readonly AccessTools.FieldRef<PlacementRegion, bool> OnBuildingsRef =
            AccessTools.FieldRefAccess<PlacementRegion, bool>("canPlaceObjectOnBuildings");

        public static bool IsPath(PlacementRegion region) => region != null && IsPathRef(region);

        public static string DescribeCell(PlacementRegion region, PlacementTile tile, string direction)
        {
            var target = region.CurrentMode == PlacementRegion.Mode.Upgrading
                ? region.CurrentStructure
                : null;
            var position = target?.Brain?.Data == null
                ? tile.GridPosition
                : target.Brain.Data.GridTilePosition;
            var grid = region.GetTileGridTile(position);
            var parts = new List<string>(7);
            if (!string.IsNullOrEmpty(direction)) parts.Add(direction);
            parts.Add($"grid x {position.x}, y {position.y}");

            if (region.CurrentMode == PlacementRegion.Mode.Demolishing)
            {
                var hovered = region.GetHoveredStructure();
                if (hovered == null)
                {
                    parts.Add(DescribeContents(region, grid));
                    parts.Add("no editable structure");
                }
                else
                {
                    parts.Add(Name(hovered.Type));
                    parts.Add(hovered.Structure_Info.CanBeMoved ? "movable with E" : "cannot be moved");
                    parts.Add(hovered.Brain.Data.IsDeletable ? "removable with R" : "cannot be removed");
                }
            }
            else if (region.CurrentMode == PlacementRegion.Mode.Upgrading)
            {
                parts.Add(target == null ? "no upgrade target" : Name(target.Type));
                parts.Add(target == null ? "unavailable" : "available");
            }
            else
            {
                parts.Add(DescribeContents(region, grid));
                parts.Add(PlacementState(region, tile));
            }

            var spoken = string.Join(", ", parts.ToArray());
            Plugin.Log.LogInfo(
                $"[build placement] mode={region.CurrentMode} type={region.StructureType} " +
                $"grid={position} rotation={region.Rotation} spoken=\"{spoken}\"");
            return spoken;
        }

        public static string Name(StructureBrain.TYPES type)
        {
            var raw = StructuresData.LocalizedName(type);
            if (RichText.IsUsableLocalization(raw, $"Structures/{type}"))
                return RichText.Clean(raw);
            return RichText.Humanise(type.ToString());
        }

        private static string PlacementState(PlacementRegion region, PlacementTile tile)
        {
            var isPath = IsPath(region);
            if (isPath && region.GetPathAtPosition() == region.StructureType)
                return "invalid, same path already here";

            if (OnlyRanchRef(region) && !RanchTilesRef(region).Contains(tile.GridPosition))
                return "invalid, outside a ranch";

            if (region.StructureType == StructureBrain.TYPES.RANCH_FENCE &&
                !FenceTilesRef(region).Contains(tile.GridPosition))
                return "invalid, not a ranch boundary";

            if (!OnlyRanchRef(region) && region.StructureType != StructureBrain.TYPES.RANCH_FENCE &&
                !isPath && RanchTilesRef(region).Contains(tile.GridPosition))
                return "invalid, inside a ranch";

            var allowObstructions = region.CurrentMode == PlacementRegion.Mode.Building ||
                                    region.CurrentMode == PlacementRegion.Mode.MultiBuild ||
                                    region.CurrentMode == PlacementRegion.Mode.Moving;
            var valid = false;
            try
            {
                valid = (bool)IsValidPlacementMethod.Invoke(
                    region, new object[] { tile.Position, !isPath, allowObstructions });
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not query build placement validity: {e.Message}");
            }

            if (valid) return "valid placement";

            var reason = Refusal(region, tile, !isPath, allowObstructions);
            return reason == null ? "invalid placement" : $"invalid placement, {reason}";
        }

        /// <summary>
        /// Why the game refused, in the game's own terms.
        ///
        /// <c>IsValidPlacement</c> returns a bare bool, and the readout could only repeat it.
        /// A live session shows what that costs: sixteen cells refused in a row, each one
        /// described as "empty, invalid placement", with nothing to search on.
        ///
        /// The confusing part has a specific cause. The check walks the structure's whole
        /// <c>Bounds</c> footprint, not just the cell under the cursor, so an anchor cell can
        /// be genuinely empty while a neighbour two tiles away is what refuses. Naming the
        /// offending cell's direction is therefore the useful half of the answer — it says
        /// which way the obstacle lies, not merely that one exists.
        ///
        /// The three failures below are the three branches of the game's own loop, in its
        /// order, so this reports the same cell it would have failed on.
        /// </summary>
        private static string Refusal(
            PlacementRegion region, PlacementTile tile, bool checkOccupied, bool allowObstructions)
        {
            var bounds = Bounds(region);
            var onBuildings = SafeOnBuildings(region);
            var anchor = new Vector2Int((int)tile.Position.x, (int)tile.Position.y);

            for (var dx = 0; dx < bounds.x; dx++)
            {
                for (var dy = 0; dy < bounds.y; dy++)
                {
                    var position = new Vector2Int(anchor.x + dx, anchor.y + dy);
                    var cell = region.GetTileGridTile(position);
                    var where = Offset(dx, dy);

                    if (cell == null) return $"outside the buildable area{where}";

                    // The game skips a cell entirely in this case, so it can never be the
                    // reason, and reporting it would send the player after a phantom.
                    if (onBuildings && !region.IsNaturalObstruction(cell.ObjectOnTile)) continue;

                    if (checkOccupied && !cell.CanPlaceStructure)
                        return $"{Occupant(cell)}{where}";

                    if (!allowObstructions && !cell.CanPlaceObstruction)
                        return $"{Obstruction(cell)}{where}";
                }
            }

            return null;
        }

        /// <summary>
        /// Which cell of the footprint refused, relative to the cursor. Omitted for the cursor
        /// cell itself, where "here" is already implied by the readout naming its contents.
        /// </summary>
        private static string Offset(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return string.Empty;

            var vertical = dy > 0 ? $"{dy} north" : dy < 0 ? $"{-dy} south" : null;
            var horizontal = dx > 0 ? $"{dx} east" : dx < 0 ? $"{-dx} west" : null;
            var offset = vertical == null
                ? horizontal
                : horizontal == null
                    ? vertical
                    : $"{vertical} {horizontal}";
            return $", {offset}";
        }

        private static string Occupant(PlacementRegion.TileGridTile cell) =>
            cell.ObjectOnTile == StructureBrain.TYPES.NONE
                ? "already occupied"
                : $"{StructureName(cell.ObjectOnTile)} in the way";

        private static string Obstruction(PlacementRegion.TileGridTile cell)
        {
            if (cell.PathID != -1) return "a path runs through";
            if (cell.ReservedForWaste) return "reserved for waste";
            if (cell.BlockNeighbouringTiles > 0) return "too close to something else";
            return cell.ObjectOnTile == StructureBrain.TYPES.NONE
                ? "blocked"
                : $"{StructureName(cell.ObjectOnTile)} in the way";
        }

        private static string StructureName(StructureBrain.TYPES type)
        {
            var localized = StructuresData.LocalizedName(type);
            return RichText.IsUsableLocalization(localized, $"Structures/{type}")
                ? RichText.Clean(localized)
                : RichText.Humanise(type.ToString());
        }

        /// <summary>The footprint, defaulting to a single cell when it cannot be read.</summary>
        internal static Vector2Int Bounds(PlacementRegion region)
        {
            try
            {
                var placement = PlacementObjectRef(region);
                if (placement != null && placement.Bounds.x > 0 && placement.Bounds.y > 0)
                    return placement.Bounds;
            }
            catch (Exception)
            {
                // A single cell is the safe assumption: it under-reports a large footprint
                // rather than inventing cells that are not part of it.
            }

            return new Vector2Int(1, 1);
        }

        private static bool SafeOnBuildings(PlacementRegion region)
        {
            try { return OnBuildingsRef(region); }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Whether a structure would fit with its anchor at this grid position. Uses the
        /// game's own check so the search and the readout can never disagree.
        /// </summary>
        internal static bool IsValid(PlacementRegion region, Vector3 position)
        {
            var isPath = IsPath(region);
            var allowObstructions = region.CurrentMode == PlacementRegion.Mode.Building ||
                                    region.CurrentMode == PlacementRegion.Mode.MultiBuild ||
                                    region.CurrentMode == PlacementRegion.Mode.Moving;
            try
            {
                return (bool)IsValidPlacementMethod.Invoke(
                    region, new object[] { position, !isPath, allowObstructions });
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string DescribeContents(
            PlacementRegion region, PlacementRegion.TileGridTile tile)
        {
            if (tile == null) return "outside buildable grid";
            if (tile.Occupied)
            {
                return tile.ObjectOnTile == StructureBrain.TYPES.NONE
                    ? "occupied"
                    : Name(tile.ObjectOnTile);
            }

            var path = region.GetPathAtPosition();
            if (path != StructureBrain.TYPES.NONE) return Name(path);
            if (tile.ReservedForWaste) return "reserved for waste";
            if (tile.Obstructed) return "obstructed";
            return "empty";
        }
    }
}
