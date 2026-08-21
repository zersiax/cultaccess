using System.Collections.Generic;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using I2.Loc;
using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>A live semantic snapshot of one base-to-dungeon door.</summary>
    internal sealed class DungeonDoorSnapshot
    {
        public int RequiredFollowers;
        public int CurrentFollowers;
        public int DisplayedFollowers;
        public bool HasFollowers;
        public bool Unlocked;
        public bool TransitionTriggerEnabled;
        public bool BlockingColliderEnabled;
        public PoiAvailability Availability;

        public bool IsOpenPassage =>
            Unlocked && TransitionTriggerEnabled && !BlockingColliderEnabled;
    }

    /// <summary>
    /// Adapts <see cref="Interaction_BaseDungeonDoor"/> across its two different jobs.
    /// Before unlocking it is an E interaction with an explicit follower threshold. After
    /// unlocking, the game disables E and enables a separate walk-through transition trigger.
    /// Treating both phases as a generic Interaction inverted both availability states.
    /// </summary>
    internal static class DungeonDoorPassage
    {
        private static readonly Dictionary<int, bool> KnownOpenStates =
            new Dictionary<int, bool>();
        private static readonly HashSet<string> ReportedFailures = new HashSet<string>();

        public static DungeonDoorSnapshot Inspect(
            Interaction_BaseDungeonDoor door,
            InteractionAvailability interactionState = null)
        {
            var snapshot = new DungeonDoorSnapshot();
            if (door == null)
            {
                snapshot.Availability = PoiAvailability.Unavailable;
                return snapshot;
            }

            snapshot.RequiredFollowers = door.FollowerCount;
            snapshot.Unlocked = door.Unlocked;
            snapshot.TransitionTriggerEnabled = ColliderIsActive(door.CollideForDoor);
            snapshot.BlockingColliderEnabled = ColliderIsActive(door.BlockingCollider);

            try
            {
                snapshot.HasFollowers = door.GetFollowerCount();
                snapshot.CurrentFollowers = CountEligibleFollowers();
                snapshot.DisplayedFollowers = DataManager.Instance?.Followers?.Count ??
                                              snapshot.CurrentFollowers;
            }
            catch (System.Exception e)
            {
                ReportFailure($"Could not read dungeon-door follower state: {e.Message}");
                snapshot.CurrentFollowers = DataManager.Instance?.Followers?.Count ?? 0;
                snapshot.DisplayedFollowers = snapshot.CurrentFollowers;
                snapshot.HasFollowers = snapshot.CurrentFollowers >= snapshot.RequiredFollowers;
            }

            if (snapshot.Unlocked)
            {
                snapshot.Availability = snapshot.IsOpenPassage
                    ? PoiAvailability.Available
                    : PoiAvailability.Unavailable;
            }
            else if (!snapshot.HasFollowers)
            {
                // Unlike a generic Interaction, this subclass exposes an exact lock: its
                // OnInteract path cannot open the door until GetFollowerCount succeeds.
                snapshot.Availability = PoiAvailability.Locked;
            }
            else
            {
                interactionState = interactionState ?? InteractionAvailability.Inspect(door);
                snapshot.Availability = interactionState.Available
                    ? PoiAvailability.Available
                    : PoiAvailability.Unavailable;
            }

            return snapshot;
        }

        public static Vector3 Aim(
            Interaction_BaseDungeonDoor door,
            DungeonDoorSnapshot snapshot = null)
        {
            if (door == null) return Vector3.zero;

            snapshot = snapshot ?? Inspect(door);
            if (snapshot.Unlocked && door.CollideForDoor != null)
            {
                // This is the collider whose OnTriggerEnter2D starts the dungeon. Its live
                // centre can be well beyond the E interaction's transform.
                return door.CollideForDoor.bounds.center;
            }

            return door.transform.position + door.ActivatorOffset;
        }

        /// <summary>
        /// Endpoint for A*. An open door's transition-trigger centre sits beyond the edge
        /// of DoorRoom's walkable graph (the observed trigger was area 100 while every
        /// approach node was area 99). Route to the game-owned interaction threshold, then
        /// let final direct guidance carry the player through the live trigger.
        /// </summary>
        public static Vector3 PathAim(Interaction_BaseDungeonDoor door)
        {
            return door == null
                ? Vector3.zero
                : door.transform.position + door.ActivatorOffset;
        }

        public static string Name(
            Interaction_BaseDungeonDoor door,
            InteractionAvailability interactionState = null)
        {
            if (door == null) return "dungeon door";

            var place = LocalisedPlaceName(door.PlaceName);
            if (place == null && door.Unlocked)
                place = interactionState?.FirstLabel;

            if (string.IsNullOrEmpty(place))
                return $"dungeon door requiring {door.FollowerCount} " +
                       (door.FollowerCount == 1 ? "follower" : "followers");

            return place.EndsWith("door", System.StringComparison.OrdinalIgnoreCase)
                ? place
                : $"{place} door";
        }

        public static string Describe(
            string name,
            DungeonDoorSnapshot snapshot,
            InteractionAvailability interactionState)
        {
            if (snapshot == null) return $"{name}, currently unavailable";

            if (snapshot.Unlocked)
                return snapshot.IsOpenPassage
                    ? $"{name}, open passage, walk through without pressing Interact"
                    : $"{name}, opened but the passage is currently blocked";

            var requirement = DungeonDoorText.Requirement(
                snapshot.CurrentFollowers, snapshot.RequiredFollowers);
            if (!snapshot.HasFollowers)
                return $"{name}, locked; {requirement}";

            if (snapshot.Availability == PoiAvailability.Available)
                return $"{name}, ready to open with Interact; {requirement}";

            var reason = interactionState?.FirstLabel;
            return string.IsNullOrEmpty(reason)
                ? $"{name}, currently unavailable"
                : $"{name}, currently unavailable: {reason}";
        }

        public static string Prompt(
            Interaction_BaseDungeonDoor door,
            InteractionAvailability interactionState = null)
        {
            interactionState = interactionState ?? InteractionAvailability.Inspect(door);
            var snapshot = Inspect(door, interactionState);
            var name = Name(door, interactionState);

            if (snapshot.Unlocked)
                return snapshot.IsOpenPassage
                    ? $"{name}, open passage. Walk through without pressing Interact"
                    : $"{name}, passage blocked";

            var requirement = DungeonDoorText.Requirement(
                snapshot.CurrentFollowers, snapshot.RequiredFollowers);
            if (!snapshot.HasFollowers) return $"{name}, locked; {requirement}";
            if (snapshot.Availability == PoiAvailability.Available)
                return $"{name}. Press Interact to open; {requirement}";

            var reason = interactionState.FirstLabel;
            return string.IsNullOrEmpty(reason)
                ? $"{name}, currently unavailable"
                : $"{name}, currently unavailable: {reason}";
        }

        public static string Arrival(
            string name,
            DungeonDoorSnapshot snapshot)
        {
            if (snapshot == null) return $"Reached {name}. It is currently unavailable.";
            if (snapshot.IsOpenPassage)
                return $"At {name}. Continue through the open doorway without pressing Interact.";
            if (!snapshot.Unlocked && snapshot.HasFollowers &&
                snapshot.Availability == PoiAvailability.Available)
                return $"Arrived at {name}. Press Interact to open it.";
            if (!snapshot.HasFollowers)
                return $"Reached {name}. It is locked; " +
                       DungeonDoorText.Requirement(
                           snapshot.CurrentFollowers, snapshot.RequiredFollowers) + ".";
            return $"Reached {name}. It is currently unavailable.";
        }

        internal static void ObservePhysicalState(Interaction_BaseDungeonDoor door)
        {
            if (door == null) return;

            var id = door.GetInstanceID();
            var open = Inspect(door).IsOpenPassage;
            if (!KnownOpenStates.TryGetValue(id, out var previous))
            {
                // Every door calls OpenDoor during initialization. Establish that baseline
                // silently so a loaded save does not announce old unlocks as new events.
                KnownOpenStates[id] = open;
                return;
            }

            if (previous == open) return;
            KnownOpenStates[id] = open;
            Navigator.RefreshAfterWorldChange();

            var name = Name(door, InteractionAvailability.Inspect(door));
            Plugin.Log.LogInfo(
                $"[dungeon door] state-changed name=\"{name}\" open={open} " +
                $"triggerEnabled={ColliderIsActive(door.CollideForDoor)} " +
                $"blockerEnabled={ColliderIsActive(door.BlockingCollider)}");
            Speaker.Say(open
                ? $"{name} opened. Walk through the doorway without pressing Interact."
                : $"{name} passage closed.",
                SpeechPriority.Now);
        }

        internal static void Shutdown()
        {
            KnownOpenStates.Clear();
            ReportedFailures.Clear();
        }

        private static int CountEligibleFollowers()
        {
            if (FollowerBrain.AllBrains == null) return 0;

            var manager = DataManager.Instance;
            var count = 0;
            foreach (var brain in FollowerBrain.AllBrains)
            {
                if (brain == null) continue;
                var info = brain._directInfoAccess;
                if (manager != null &&
                    (manager.Followers_Dead.Contains(info) ||
                     manager.Followers_Recruit.Contains(info)))
                    continue;
                count++;
            }
            return count;
        }

        private static bool ColliderIsActive(Collider2D collider)
        {
            return collider != null && collider.enabled &&
                   collider.gameObject.activeInHierarchy;
        }

        private static string LocalisedPlaceName(string term)
        {
            var cleanTerm = RichText.Clean(term);
            if (cleanTerm.Length == 0) return null;

            try
            {
                var translated = LocalizationManager.GetTranslation(term);
                if (!RichText.IsUsableLocalization(translated, term)) return null;
                var clean = RichText.Clean(translated);
                return clean.Length == 0 ? null : clean;
            }
            catch (System.Exception e)
            {
                ReportFailure($"Could not localize dungeon door term '{term}': {e.Message}");
                return null;
            }
        }

        private static void ReportFailure(string message)
        {
            if (ReportedFailures.Add(message)) Plugin.Log.LogWarning(message);
        }
    }

    /// <summary>Observe the exact point where the game has applied door collider state.</summary>
    [HarmonyPatch(typeof(Interaction_BaseDungeonDoor), "OpenDoor")]
    internal static class BaseDungeonDoorPhysicalStatePatch
    {
        [HarmonyPostfix]
        private static void AfterOpenDoor(Interaction_BaseDungeonDoor __instance)
        {
            DungeonDoorPassage.ObservePhysicalState(__instance);
        }
    }
}
