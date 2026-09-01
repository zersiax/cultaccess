using UnityEngine;
using MMRoomGeneration;

namespace CultAccess.Navigation
{
    /// <summary>What kind of thing a point of interest is, for filtering and wording.</summary>
    public enum PoiKind
    {
        Exit,
        Interactable,
        Follower,
        Character,
        Enemy,
    }

    public enum PoiAvailability
    {
        Available,
        Unavailable,
        Locked,
    }

    /// <summary>A thing in the world worth walking to.</summary>
    public class PointOfInterest
    {
        public Transform Transform;
        public string Name;
        public PoiKind Kind;
        public Interaction Interaction;
        public Door Door;
        internal InteractionRole InteractionRole;
        internal ResourceRole ResourceRole;
        internal bool IsBulkResource;
        internal BaseGoopDoor BasePassageGate;
        internal EnterBuilding BuildingEntranceTrigger;

        /// <summary>
        /// The sealed door to a dungeon's boss. An `Interaction`, but not one you interact
        /// with: `Interaction_TempleBossDoor.OnTriggerEnter2D` changes room when the player
        /// walks into it, and `Interactable` is false for almost its whole life.
        ///
        /// So it reached the player as "Temple Boss Door, currently unavailable" — the exact
        /// shape principle 3 warns about, a UI-facing availability read as the truth when the
        /// real state was "walk through it". Its own `Unlocked` field is the authority.
        /// </summary>
        internal Interaction_TempleBossDoor TempleBossDoor;
        internal Interaction_BaseDungeonDoor DungeonDoor;
        internal Interaction_WeaponSelectionPodium WeaponPodium;
        internal Vector3? AimPositionOverride;
        internal bool IsReturnPassage;
        internal bool IsPendingRecruit;

        private InteractionAvailability _interactionState;
        private int _interactionStateFrame = -1;
        private DungeonDoorSnapshot _dungeonDoorState;
        private int _dungeonDoorStateFrame = -1;

        public Vector3 Position => Transform == null ? Vector3.zero : Transform.position;
        public bool Alive => Transform != null && Transform.gameObject.activeInHierarchy;

        /// <summary>
        /// Interaction triggers are centred on ActivatorOffset. Doors transition from their
        /// own trigger, so their transform is the destination rather than PlayerPosition,
        /// which the game uses to place a player back inside after cancelling an exit UI.
        /// </summary>
        public Vector3 AimPosition =>
            DungeonDoor != null ? DungeonDoorPassage.Aim(DungeonDoor, CurrentDungeonDoorState) :
            BuildingEntranceTrigger != null ? BuildingEntrance.Aim(BuildingEntranceTrigger) :
            AimPositionOverride ??
            (Interaction != null ? Interaction.transform.position + Interaction.ActivatorOffset : Position);

        /// <summary>
        /// Graph endpoint, which can differ from the physical arrival point. Open base
        /// dungeon doors route to their reachable threshold, then continue through a
        /// transition trigger whose centre is intentionally outside the connected graph.
        /// </summary>
        public Vector3 PathPosition =>
            DungeonDoor != null && CurrentDungeonDoorState.IsOpenPassage
                ? DungeonDoorPassage.PathAim(DungeonDoor)
                : AimPosition;

        internal bool HasPostPathApproach =>
            DungeonDoor != null && CurrentDungeonDoorState.IsOpenPassage;

        internal bool IsAutomaticPassage =>
            BasePassageGate != null ||
            BuildingEntranceTrigger != null ||
            (TempleBossDoor != null && TempleBossDoorUnlocked) ||
            (DungeonDoor != null && CurrentDungeonDoorState.Unlocked);

        /// <summary>
        /// Read through Harmony rather than directly: `Unlocked` is private, and it is the
        /// only thing that separates a door you walk through from one that is genuinely shut.
        /// Defaults to shut if it cannot be read, because inviting the player to walk into a
        /// sealed door is the worse of the two mistakes.
        /// </summary>
        internal bool TempleBossDoorUnlocked
        {
            get
            {
                if (TempleBossDoor == null) return false;

                try
                {
                    return TempleBossDoorUnlockedField != null &&
                           TempleBossDoorUnlockedField(TempleBossDoor);
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning(
                        $"[temple door] could not read its unlocked state: {e.Message}");
                    return false;
                }
            }
        }

        private static readonly HarmonyLib.AccessTools.FieldRef<Interaction_TempleBossDoor, bool>
            TempleBossDoorUnlockedField = BuildTempleBossDoorAccessor();

        private static HarmonyLib.AccessTools.FieldRef<Interaction_TempleBossDoor, bool>
            BuildTempleBossDoorAccessor()
        {
            try
            {
                var field = HarmonyLib.AccessTools.Field(
                    typeof(Interaction_TempleBossDoor), "Unlocked");
                if (field != null)
                    return HarmonyLib.AccessTools
                        .FieldRefAccess<Interaction_TempleBossDoor, bool>(field);

                Plugin.Log.LogWarning(
                    "[temple door] Interaction_TempleBossDoor has no Unlocked field; it will " +
                    "be described as sealed rather than as a passage.");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[temple door] could not bind Unlocked: {e.Message}");
            }

            return null;
        }

        internal InteractionAvailability CurrentInteractionState
        {
            get
            {
                if (_interactionStateFrame == Time.frameCount && _interactionState != null)
                    return _interactionState;

                _interactionStateFrame = Time.frameCount;
                return _interactionState = InteractionAvailability.Inspect(Interaction);
            }
        }

        internal DungeonDoorSnapshot CurrentDungeonDoorState
        {
            get
            {
                if (_dungeonDoorStateFrame == Time.frameCount && _dungeonDoorState != null)
                    return _dungeonDoorState;

                _dungeonDoorStateFrame = Time.frameCount;
                return _dungeonDoorState = DungeonDoorPassage.Inspect(
                    DungeonDoor, CurrentInteractionState);
            }
        }

        public PoiAvailability Availability
        {
            get
            {
                if (Door != null)
                {
                    if (DoorIsUnavailable(Door)) return PoiAvailability.Unavailable;
                    return DoorIsLocked(Door) ? PoiAvailability.Locked : PoiAvailability.Available;
                }
                if (BasePassageGate != null)
                    return BasePassage.IsOpen(BasePassageGate)
                        ? PoiAvailability.Available
                        : PoiAvailability.Unavailable;
                if (BuildingEntranceTrigger != null)
                    return BuildingEntrance.IsOpen(BuildingEntranceTrigger)
                        ? PoiAvailability.Available
                        : PoiAvailability.Unavailable;
                // Before the generic interaction path, which would read its Interactable and
                // conclude "unavailable" about a door that is standing open. Unlocked is the
                // door's own authority; locked rather than unavailable when it is not, because
                // it opens later in the same run rather than being permanently out of reach.
                if (TempleBossDoor != null)
                    return TempleBossDoorUnlocked
                        ? PoiAvailability.Available
                        : PoiAvailability.Locked;
                if (DungeonDoor != null)
                    return CurrentDungeonDoorState.Availability;
                if (WeaponPodium != null)
                    return WeaponPodiumTarget.Inspect(
                        WeaponPodium, CurrentInteractionState).Available
                        ? PoiAvailability.Available
                        : PoiAvailability.Unavailable;
                if (IsPendingRecruit)
                    return PoiAvailability.Available;
                if (Interaction != null)
                    return CurrentInteractionState.Available
                        ? PoiAvailability.Available
                        : PoiAvailability.Unavailable;
                return PoiAvailability.Available;
            }
        }

        /// <summary>Name plus kind and only the state information that affects use.</summary>
        public string Describe()
        {
            // Followers are the one kind whose entry is worth more than a name and a noun.
            // A sighted player reads their form, their level, the warning icons over their
            // head and whether they are holding a finished quest, all from across the base;
            // everything here is derived from the same fields those visuals are drawn from.
            if (Kind == PoiKind.Follower && Interaction is interaction_FollowerInteraction follower)
                return FollowerTarget.Describe(
                    follower, Name, Plugin.Verbosity.Value != UI.Verbosity.Low);

            var description = Kind == PoiKind.Interactable || Kind == PoiKind.Exit
                ? Name
                : $"{Name}, {Kind.ToString().ToLowerInvariant()}";

            if (Door != null)
            {
                switch (Availability)
                {
                    case PoiAvailability.Locked: return $"{description}, locked";
                    case PoiAvailability.Unavailable: return $"{description}, currently unavailable";
                    default: return $"{description}, open";
                }
            }

            // Before the Interaction branch: an enterable building's doorway carries no
            // Interaction at all, and the wording has to say that walking in is the action.
            if (BuildingEntranceTrigger != null)
                return BuildingEntrance.Describe(description, BuildingEntranceTrigger);

            if (Interaction != null)
            {
                if (BasePassageGate != null)
                {
                    if (Availability == PoiAvailability.Available)
                        return $"{description}, open passage, walk through without pressing Interact";

                    var reason = CurrentInteractionState.FirstLabel;
                    return string.IsNullOrEmpty(reason)
                        ? $"{description}, blocked"
                        : $"{description}, blocked: {reason}";
                }

                if (DungeonDoor != null)
                    return DungeonDoorPassage.Describe(
                        description, CurrentDungeonDoorState, CurrentInteractionState);

                if (WeaponPodium != null)
                    return WeaponPodiumTarget.Describe(
                        description,
                        WeaponPodiumTarget.Inspect(WeaponPodium, CurrentInteractionState));

                var status = CurrentInteractionState.SpeechStatus;
                if (!string.IsNullOrEmpty(status)) return $"{description}, {status}";
            }

            return description;
        }

        /// <summary>
        /// The live Interactor is authoritative for actionable interactions. Dormant
        /// conversations and locked doors cannot become CurrentInteraction, so reaching
        /// their physical location is the only arrival condition that can truthfully apply.
        /// </summary>
        public bool HasArrived(Transform player, Interactor interactor)
        {
            if (player == null) return false;

            if (DungeonDoor != null)
            {
                var state = CurrentDungeonDoorState;
                if (state.IsOpenPassage)
                    return Vector3.Distance(player.position, AimPosition) <= 1f;

                if (!state.Unlocked && state.HasFollowers &&
                    state.Availability == PoiAvailability.Available)
                {
                    if (interactor != null) return interactor.CurrentInteraction == Interaction;
                    return Vector3.Distance(player.position, AimPosition) < Interaction.ActivateDistance;
                }

                return Vector3.Distance(player.position, AimPosition) <= 1f;
            }

            if (WeaponPodium != null)
            {
                if (interactor != null) return interactor.CurrentInteraction == Interaction;
                return Vector3.Distance(player.position, AimPosition) < Interaction.ActivateDistance;
            }

            // Same arrival rule as a doorway: the trigger fires on contact, so reaching the
            // collider centre is the only condition that can truthfully apply.
            if (BuildingEntranceTrigger != null)
                return Vector3.Distance(player.position, AimPosition) <= 1f;

            if (IsPendingRecruit)
            {
                // The real FollowerRecruit appears while approaching this authored spawn
                // point. Prefer that live prompt as the arrival signal once it exists.
                if (interactor?.CurrentInteraction is FollowerRecruit) return true;
                return Vector3.Distance(player.position, AimPosition) <= 1.75f;
            }

            if (Interaction != null && CurrentInteractionState.Available)
            {
                if (interactor != null) return interactor.CurrentInteraction == Interaction;
                return Vector3.Distance(player.position, AimPosition) < Interaction.ActivateDistance;
            }

            if (Door != null)
                return Vector3.Distance(player.position, AimPosition) <= 1f;

            if (BasePassageGate != null)
                return Vector3.Distance(player.position, AimPosition) <= 1f;

            if (Interaction != null)
                return Vector3.Distance(player.position, AimPosition) <
                       Mathf.Clamp(Interaction.ActivateDistance, 0.5f, 1.75f);

            return Vector3.Distance(player.position, AimPosition) <= 1.75f;
        }

        public string ArrivalAnnouncement()
        {
            if (DungeonDoor != null)
                return DungeonDoorPassage.Arrival(Name, CurrentDungeonDoorState);

            if (WeaponPodium != null)
                return WeaponPodiumTarget.Arrival(
                    Name,
                    WeaponPodiumTarget.Inspect(WeaponPodium, CurrentInteractionState));

            if (BuildingEntranceTrigger != null)
                return BuildingEntrance.Arrival(Name, BuildingEntranceTrigger);

            if (IsPendingRecruit)
                return "At the indoctrination platform. A new follower will appear nearby; " +
                       "press Interact when Indoctrinate is announced.";

            switch (Availability)
            {
                case PoiAvailability.Locked:
                    return $"Reached {Name}. It is locked.";
                case PoiAvailability.Unavailable:
                    return BasePassageGate != null
                        ? $"Reached {Name}. It is blocked."
                        : $"Reached {Name}. It is currently unavailable.";
                default:
                    return Door != null || BasePassageGate != null
                        ? $"At {Name}. Continue through the doorway."
                        : $"Arrived at {Name}.";
            }
        }

        internal static bool DoorIsLocked(Door door)
        {
            if (door == null) return false;
            var controller = door.RoomLockController;
            if (controller == null || !controller.gameObject.activeInHierarchy) return false;

            // DoorUp/DoorDown write Open and the blocker together; ForcedLocked prevents
            // DoorDown from opening. These are explicit game states, not a distance guess.
            return !controller.Open || controller.ForcedLocked;
        }

        internal static bool DoorIsUnavailable(Door door)
        {
            if (door == null) return true;

            // Door.OnTriggerEnter2D explicitly returns for these connection types instead
            // of changing room. This is an unavailable exit state, not a lock inference.
            if (door.ConnectionType == GenerateRoom.ConnectionTypes.False ||
                door.ConnectionType == GenerateRoom.ConnectionTypes.LeaderBoss)
                return true;

            // The forward door of a miniboss room is inert until that dungeon is completed.
            // Nothing on the door itself says so — the state lives on a sibling teleporter —
            // so without this we would route the player to a door that does nothing.
            if (EndOfRunDoor.IsInert(door)) return true;

            // Entrance/Exit opens the world map outside a dungeon. In a dungeon the exact
            // trigger branch exits without doing anything unless one of its explicit return
            // destinations is configured.
            if (door.ConnectionType != GenerateRoom.ConnectionTypes.Entrance &&
                door.ConnectionType != GenerateRoom.ConnectionTypes.Exit)
                return false;

            if (door.ForceReturnToBase || door.ForceReturnToDoorRoom ||
                door.ForceReturnToLambTown || door.ForceReturnToGraveyard)
                return false;

            try
            {
                return GameManager.IsDungeon(PlayerFarming.Location);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not evaluate door destination state: {e.Message}");
                return false;
            }
        }
    }
}
