using CultAccess.Speech;
using HarmonyLib;
using UnityEngine;
using ScreenCompass = CultAccess.Navigation.Compass;

namespace CultAccess.Building
{
    /// <summary>
    /// Non-visual view of the game's existing base-building cursor. This never moves the
    /// cursor or confirms an action; it reports the live tile and the same validity gates
    /// that <see cref="PlacementRegion"/> checks when E is pressed.
    /// </summary>
    internal static class PlacementAnnouncer
    {
        private static readonly AccessTools.FieldRef<PlacementRegion, PlacementTile> CurrentTileRef =
            AccessTools.FieldRefAccess<PlacementRegion, PlacementTile>("CurrentTile");
        private static PlacementRegion _activeRegion;
        private static PlacementTile _lastTile;
        private static Vector3 _lastWorldPosition;
        private static int _lastRotation;
        private static PlacementRegion.Mode _lastMode;
        private static StructureBrain.TYPES _lastStructureType;
        private static Structure _lastUpgradeTarget;
        private static bool _subscribed;

        public static bool IsActive =>
            PlacementRegion.Instance != null && PlacementObject.Instance != null &&
            PlacementRegion.Instance.CurrentMode != PlacementRegion.Mode.None;

        public static void Initialize()
        {
            if (_subscribed) return;
            PlacementRegion.OnNewBuilding += OnNewBuilding;
            _subscribed = true;
        }

        public static void Shutdown()
        {
            if (_subscribed)
                PlacementRegion.OnNewBuilding -= OnNewBuilding;
            _subscribed = false;
            Reset();
        }

        public static void Tick()
        {
            if (!Plugin.SpeechEnabled.Value) return;

            var region = PlacementRegion.Instance;
            if (!IsActive || region == null)
            {
                Reset();
                return;
            }

            PlacementTile tile;
            try { tile = CurrentTileRef(region); }
            catch { return; }
            if (tile == null) return;

            if (region != _activeRegion || region.CurrentMode != _lastMode ||
                region.StructureType != _lastStructureType)
            {
                _activeRegion = region;
                _lastTile = null;
                _lastRotation = region.Rotation;
                _lastMode = region.CurrentMode;
                _lastStructureType = region.StructureType;
                SpeakEntry(region, tile);
                Remember(region, tile);
                return;
            }

            if (region.Rotation != _lastRotation)
            {
                _lastRotation = region.Rotation;
                Speaker.SayParts(
                    SpeechPriority.Now, "rotated", PlacementDescriber.DescribeCell(region, tile, null));
                Remember(region, tile);
                return;
            }

            if (region.CurrentMode == PlacementRegion.Mode.Upgrading &&
                region.CurrentStructure != _lastUpgradeTarget)
            {
                string upgradeDirection = null;
                if (_lastUpgradeTarget != null && region.CurrentStructure != null)
                {
                    ScreenCompass.TryDescribe(
                        _lastWorldPosition, region.CurrentStructure.transform.position,
                        out upgradeDirection, out _);
                }

                Speaker.Say(
                    PlacementDescriber.DescribeCell(region, tile, upgradeDirection), SpeechPriority.Now);
                Remember(region, tile);
                return;
            }

            if (tile == _lastTile) return;

            string direction = null;
            if (_lastTile != null)
                ScreenCompass.TryDescribe(_lastWorldPosition, tile.transform.position, out direction, out _);

            Speaker.Say(PlacementDescriber.DescribeCell(region, tile, direction), SpeechPriority.Now);
            Remember(region, tile);
        }

        public static void AnnounceCurrent()
        {
            var region = PlacementRegion.Instance;
            if (!IsActive || region == null)
            {
                Speaker.Say("No placement cursor active.");
                return;
            }

            var tile = CurrentTileRef(region);
            Speaker.Say(tile == null
                ? "Placement cursor not ready."
                : PlacementDescriber.DescribeCell(region, tile, null));
        }

        private static void SpeakEntry(PlacementRegion region, PlacementTile tile)
        {
            var type = region.StructureType;
            var name = PlacementDescriber.Name(type);
            var placement = PlacementObject.Instance;
            var footprint = placement == null || region.CurrentMode == PlacementRegion.Mode.Demolishing
                ? string.Empty
                : $"footprint {placement.Bounds.x} by {placement.Bounds.y}";

            string controls;
            switch (region.CurrentMode)
            {
                case PlacementRegion.Mode.Demolishing:
                    controls = "Edit buildings. W A S D moves. E picks up a movable structure. " +
                               "R removes a deletable structure. F exits. F12 repeats this cell";
                    break;
                case PlacementRegion.Mode.Upgrading:
                    controls = "W A S D chooses a structure to upgrade. E confirms. F cancels. " +
                               "F12 repeats the selection";
                    break;
                default:
                    if (PlacementDescriber.IsPath(region))
                    {
                        controls = "W A S D moves. Hold E to place paths. Hold R to remove paths. " +
                                   "F cancels. F12 repeats this cell";
                    }
                    else
                    {
                        var rotate = StructuresData.CanBeFlipped(type) ? " R rotates." : string.Empty;
                        controls = $"W A S D moves. E places.{rotate} F cancels. F12 repeats this cell";
                    }
                    break;
            }

            var lead = region.CurrentMode switch
            {
                PlacementRegion.Mode.Moving => $"Moving {name}",
                PlacementRegion.Mode.Upgrading => $"Upgrading to {name}",
                _ => $"Placing {name}",
            };

            Speaker.SayParts(
                SpeechPriority.Now,
                region.CurrentMode == PlacementRegion.Mode.Demolishing ? controls : lead,
                footprint,
                region.CurrentMode == PlacementRegion.Mode.Demolishing ? null : controls,
                PlacementDescriber.DescribeCell(region, tile, null));
        }

        private static void OnNewBuilding(StructureBrain.TYPES type)
        {
            // The event fires synchronously after the build site has been created. Defer the
            // scan so its Interaction.Start has populated the localized Build label.
            BuildingTargetRefresh.Schedule($"placed-{type}");

            if (!Plugin.SpeechEnabled.Value) return;

            var region = PlacementRegion.Instance;
            PlacementTile tile = null;
            if (region != null)
            {
                try { tile = CurrentTileRef(region); } catch { /* transition */ }
            }

            var location = tile == null
                ? string.Empty
                : $"grid x {tile.GridPosition.x}, y {tile.GridPosition.y}";
            Speaker.SayParts(SpeechPriority.Now, "Placed " + PlacementDescriber.Name(type), location);
            Plugin.Log.LogInfo($"[build placed] type={type} grid={tile?.GridPosition.ToString() ?? "unknown"}");
        }

        private static void Remember(PlacementRegion region, PlacementTile tile)
        {
            _lastTile = tile;
            _lastUpgradeTarget = region.CurrentMode == PlacementRegion.Mode.Upgrading
                ? region.CurrentStructure
                : null;
            _lastWorldPosition = _lastUpgradeTarget == null
                ? tile.transform.position
                : _lastUpgradeTarget.transform.position;
        }

        private static void Reset()
        {
            _activeRegion = null;
            _lastTile = null;
            _lastWorldPosition = Vector3.zero;
            _lastRotation = 0;
            _lastMode = PlacementRegion.Mode.None;
            _lastStructureType = StructureBrain.TYPES.NONE;
            _lastUpgradeTarget = null;
        }
    }
}
