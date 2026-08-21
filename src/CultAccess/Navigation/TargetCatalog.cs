using System.Collections.Generic;
using CultAccess.Speech;
using MMRoomGeneration;
using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Owns scan results, category filtering and selection. Route following stays in
    /// <see cref="Navigator"/> so neither concern grows into another oversized class.
    /// </summary>
    internal static class TargetCatalog
    {
        private static readonly List<PointOfInterest> Scanned = new List<PointOfInterest>();
        private static readonly List<PointOfInterest> Targets = new List<PointOfInterest>();

        private static int _index = -1;
        private static int _categoryIndex;

        private static TargetCategory CurrentCategory =>
            TargetCategoryPolicy.At(_categoryIndex);

        public static PointOfInterest Selected =>
            _index >= 0 && _index < Targets.Count ? Targets[_index] : null;

        public static void Refresh(bool announce = true, bool preserveSelection = false)
        {
            var previous = preserveSelection ? Selected : null;
            var player = NavigatorPlayer.Resolve();
            if (player == null)
            {
                if (announce) Speaker.Say("Cannot find the player character.");
                Plugin.Log.LogWarning(
                    "Player lookup failed: PlayerFarming.Instance, the Player tag, and " +
                    "PlayerPrisonerController all came back empty.");
                return;
            }

            Scanned.Clear();
            Scanned.AddRange(WorldScanner.Scan(player.position));
            ApplyCategory();
            if (previous != null) RestoreSelection(previous);

            if (!announce) return;
            if (Targets.Count == 0)
            {
                Speaker.Say(EmptyCategoryMessage());
                return;
            }

            var exit = CurrentCategory == TargetCategory.Everything ? ExitSummary(player) : null;
            var gate = StoryGateNotice();
            Speaker.Say(
                $"{Targets.Count} in {CategoryName}. {gate}{exit}{Describe(Targets[0], player)}");
        }

        private static void RestoreSelection(PointOfInterest previous)
        {
            for (var i = 0; i < Targets.Count; i++)
            {
                var candidate = Targets[i];
                if ((previous.Interaction != null &&
                     candidate.Interaction == previous.Interaction) ||
                    (previous.Door != null && candidate.Door == previous.Door) ||
                    (previous.Interaction == null && previous.Door == null &&
                     candidate.Transform == previous.Transform))
                {
                    _index = i;
                    return;
                }
            }
        }

        /// <summary>
        /// Step the category filter. Wraps in both directions, so a player who has gone one
        /// too far does not have to walk the whole list round again to get back.
        /// </summary>
        public static void CycleCategory(int direction = 1)
        {
            var count = TargetCategoryPolicy.Count;
            _categoryIndex = ((_categoryIndex + direction) % count + count) % count;

            if (Scanned.Count == 0)
            {
                Refresh();
                return;
            }

            ApplyCategory();
            if (Targets.Count == 0)
            {
                Speaker.Say(EmptyCategoryMessage());
                return;
            }

            Speaker.Say($"{CategoryName}, {Targets.Count}. {Describe(Targets[0])}");
        }

        public static void Cycle(int direction)
        {
            if (Targets.Count == 0)
            {
                Refresh();
                return;
            }

            _index += direction;
            if (_index < 0) _index = Targets.Count - 1;
            if (_index >= Targets.Count) _index = 0;

            var target = Targets[_index];
            if (!target.Alive)
            {
                Refresh();
                return;
            }

            // Name first, position last. Stepping through a list, the name is what the player
            // is listening for and the position is confirmation; leading with the number makes
            // every entry start the same way and delays the only part that differs.
            Speaker.Say($"{Describe(target)}, {_index + 1} of {Targets.Count}");
        }

        public static void AnnounceCategory()
        {
            Speaker.Say(Targets.Count == 0
                ? EmptyCategoryMessage()
                : $"{CategoryName}, {Targets.Count} nearby.");
        }

        /// <summary>
        /// An empty list during a story beat is the case that read as a softlock: the game
        /// blanks every interaction label at once, so there is genuinely nothing to press, and
        /// saying only "nothing nearby" leaves the player with no idea whether the mod broke.
        /// Name the cause and follow it with what the game is actually waiting for.
        /// </summary>
        private static string EmptyCategoryMessage()
        {
            var text = $"Nothing nearby in {CategoryName}.";
            if (!StoryLabelGate.Closed) return text;

            text += " A story moment is in progress, so nothing can be used yet.";
            var step = Status.OnboardingTracker.CurrentStep();
            return step.Length == 0 ? text : $"{text} {step}.";
        }

        /// <summary>Short prefix for a populated list while the same gate is closed.</summary>
        private static string StoryGateNotice() =>
            StoryLabelGate.Closed
                ? "A story moment is in progress; nothing can be used yet. "
                : null;

        public static void AnnounceSelected()
        {
            if (Selected == null) Speaker.Say("No target selected.");
            else Speaker.Say(Describe(Selected));
        }

        public static string Describe(PointOfInterest target)
        {
            return Describe(target, NavigatorPlayer.Resolve());
        }

        private static string Describe(PointOfInterest target, Transform player)
        {
            if (target == null) return "No target";
            if (player == null || !target.Alive) return target.Describe();

            if (!Compass.TryDescribe(player.position, target.AimPosition, out var bearing, out var distance))
                return $"{target.Describe()}, {Compass.DescribeDistance(distance)}";

            return $"{target.Describe()}, {Compass.DescribeDistance(distance)}, {bearing}";
        }

        private static void ApplyCategory()
        {
            Targets.Clear();

            if (CurrentCategory == TargetCategory.Everything ||
                CurrentCategory == TargetCategory.ActionsNow)
            {
                AddCuratedCategory();
                FinishCategory();
                return;
            }

            if (CurrentCategory == TargetCategory.TravelAndExits)
            {
                AddTravelTargets();
                FinishCategory();
                return;
            }

            foreach (var poi in Scanned)
            {
                if (!poi.Alive) continue;
                if (!TargetCategoryPolicy.Matches(CurrentCategory, poi)) continue;
                Targets.Add(poi);
            }
            FinishCategory();
        }

        private static void FinishCategory()
        {
            _index = Targets.Count > 0 ? 0 : -1;

            var scannedBulk = 0;
            var shownBulk = 0;
            foreach (var poi in Scanned)
                if (poi.Alive && poi.IsBulkResource) scannedBulk++;
            foreach (var poi in Targets)
                if (poi.IsBulkResource) shownBulk++;

            Diagnostics.NavigationDiagnostics.LogTargetCategory(
                CategoryName, Scanned.Count, Targets.Count, scannedBulk, shownBulk);
        }

        /// <summary>
        /// Keep the broad and immediately-actionable views useful without losing information.
        /// Repeated natural-resource nodes are the only things collapsed: one representative
        /// per kind, preferring the nearest currently usable node. The Resources category
        /// remain exhaustive.
        /// </summary>
        private static void AddCuratedCategory()
        {
            var representatives = new Dictionary<ResourceRole, PointOfInterest>();
            foreach (var poi in Scanned)
            {
                if (!IncludedInCuratedCategory(poi) || !poi.IsBulkResource) continue;

                if (!representatives.TryGetValue(poi.ResourceRole, out var current) ||
                    current.Availability != PoiAvailability.Available &&
                    poi.Availability == PoiAvailability.Available)
                    representatives[poi.ResourceRole] = poi;
            }

            var retainedResources = new HashSet<PointOfInterest>(representatives.Values);
            foreach (var poi in Scanned)
            {
                if (!IncludedInCuratedCategory(poi)) continue;
                if (poi.IsBulkResource && !retainedResources.Contains(poi)) continue;
                Targets.Add(poi);
            }
        }

        private static bool IncludedInCuratedCategory(PointOfInterest poi)
        {
            if (!poi.Alive) return false;
            return CurrentCategory == TargetCategory.Everything ||
                   TargetCategoryPolicy.Matches(CurrentCategory, poi);
        }

        private static void AddTravelTargets()
        {
            // The entrance is normally the door behind the player. Keep it available,
            // but put forward/progression doors first in the dedicated travel category.
            AddExitTargets(includeEntrances: false);

            AddTravelInteractions(PoiAvailability.Available, returnPassages: false);
            AddTravelInteractions(PoiAvailability.Unavailable, returnPassages: false);
            AddTravelInteractions(PoiAvailability.Locked, returnPassages: false);
            AddTravelInteractions(PoiAvailability.Available, returnPassages: true);
            AddTravelInteractions(PoiAvailability.Unavailable, returnPassages: true);
            AddTravelInteractions(PoiAvailability.Locked, returnPassages: true);

            AddExitTargets(includeEntrances: true);
        }

        private static void AddTravelInteractions(
            PoiAvailability availability, bool returnPassages)
        {
            foreach (var poi in Scanned)
            {
                if (!poi.Alive || poi.InteractionRole != InteractionRole.Travel) continue;
                if (poi.Availability != availability || poi.IsReturnPassage != returnPassages)
                    continue;
                Targets.Add(poi);
            }
        }

        private static void AddExitTargets(bool includeEntrances)
        {
            foreach (var poi in Scanned)
            {
                if (!poi.Alive || poi.Kind != PoiKind.Exit || poi.Door == null) continue;
                var isEntrance = poi.Door.ConnectionType == GenerateRoom.ConnectionTypes.Entrance;
                if (isEntrance != includeEntrances) continue;
                Targets.Add(poi);
            }
        }

        private static string CategoryName => TargetCategoryPolicy.Name(CurrentCategory);

        /// <summary>
        /// Dungeon doors were previously absent entirely. Point out the nearest forward
        /// door on every unfiltered rescan without silently changing the selected target.
        /// </summary>
        private static string ExitSummary(Transform player)
        {
            PointOfInterest nearest = null;
            var count = 0;
            foreach (var poi in Scanned)
            {
                if (poi.Door == null ||
                    poi.Door.ConnectionType == GenerateRoom.ConnectionTypes.Entrance)
                    continue;

                count++;
                if (nearest == null ||
                    nearest.Availability != PoiAvailability.Available &&
                    poi.Availability == PoiAvailability.Available)
                    nearest = poi;
            }

            if (nearest == null) return string.Empty;
            var prefix = count == 1 ? "Exit nearby: " : $"{count} exits nearby; nearest: ";
            return $"{prefix}{Describe(nearest, player)}. Selected target: ";
        }
    }
}
