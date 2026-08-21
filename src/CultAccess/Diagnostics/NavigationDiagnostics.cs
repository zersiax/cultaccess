using CultAccess.Navigation;
using HarmonyLib;
using UnityEngine;

namespace CultAccess.Diagnostics
{
    /// <summary>
    /// Log-only evidence for navigation, beacon projection and interaction input. The
    /// snapshots deliberately include both world and screen coordinates so an apparent
    /// left/right inversion can be separated from a route-waypoint change.
    /// </summary>
    internal static class NavigationDiagnostics
    {
        public static bool Enabled = true;

        private static float _nextSnapshotAt;

        public static void Tick()
        {
            if (!Enabled || Time.unscaledTime < _nextSnapshotAt) return;

            // Re-armed here, before anything can return early. It used to be set after the
            // player lookup, so a frame with no player left the throttle un-armed and the
            // whole snapshot ran again on the very next frame — and every frame after that,
            // for as long as there was no player. A once-a-second diagnostic quietly became
            // a per-frame one, and the lookup it repeats is three scene-wide searches.
            _nextSnapshotAt = Time.unscaledTime + 1f;

            var target = Navigator.TrackedTarget;
            var player = NavigatorPlayer.Resolve();
            if (player == null) return;
            if (target != null)
            {
                var aim = target.AimPosition;
                var path = target.PathPosition;
                var waypoint = Navigator.TrackedWaypoint;
                var interactor = NavigatorPlayer.ResolveInteractor();
                var current = interactor?.CurrentInteraction;
                var farming = PlayerFarming.Instance;

                Plugin.Log.LogInfo(
                    $"[nav coords] target=\"{Safe(target.Name)}\" kind={target.Kind} " +
                    $"routeState={(Navigator.WaitingForRoute ? "pending" : "active")} " +
                    $"graphRevision={Navigator.GraphRevision} " +
                    $"availability={target.Availability} playerWorld={Vector(player.position)} " +
                    $"aimWorld={Vector(aim)} pathWorld={Vector(path)} " +
                    $"worldDelta={Vector(aim - player.position)} " +
                    $"distance={Vector3.Distance(player.position, aim):0.00} " +
                    $"waypointWorld={(waypoint.HasValue ? Vector(waypoint.Value) : "none")} " +
                    $"waypointDelta={(waypoint.HasValue ? Vector(waypoint.Value - player.position) : "none")} " +
                    $"interactorWorld={(interactor == null ? "none" : Vector(interactor.transform.position))} " +
                    $"mainPlayerWorld={(farming == null ? "none" : Vector(farming.transform.position))} " +
                    $"currentInteraction=\"{InteractionName(current)}\" " +
                    $"currentIsTarget={target.Interaction != null && current == target.Interaction}");
            }

            if (!Audio.Beacon.TryGetTargetPosition(out var beacon, out var mode)) return;

            var camera = Camera.main;
            if (camera == null)
            {
                Plugin.Log.LogInfo(
                    $"[beacon coords] mode={mode} world={Vector(beacon)} camera=none");
                return;
            }

            var from = camera.WorldToScreenPoint(player.position);
            var to = camera.WorldToScreenPoint(beacon);
            var delta = to - from;
            var pan = Mathf.Clamp(delta.x / (Screen.width * 0.35f), -1f, 1f);
            var vertical = Mathf.Clamp(delta.y / (Screen.height * 0.35f), -1f, 1f);
            var pitch = Mathf.Lerp(0.7f, 1.7f, (vertical + 1f) * 0.5f);

            Plugin.Log.LogInfo(
                $"[beacon coords] mode={mode} world={Vector(beacon)} " +
                $"playerScreen={Vector(from)} targetScreen={Vector(to)} " +
                $"screenDelta={Vector(delta)} pan={pan:0.00} vertical={vertical:0.00} " +
                $"pitch={pitch:0.00}");
        }

        public static void LogReachability(
            string routeEvent,
            PointOfInterest target,
            ReachabilitySnapshot current,
            ReachabilitySnapshot baseline,
            RouteBlockerInspection blockers,
            bool targetLocked,
            int graphRevision)
        {
            if (!Enabled || target == null || current == null) return;

            var playerDelta = baseline == null
                ? 0f
                : Vector3.Distance(baseline.PlayerPosition, current.PlayerPosition);
            var targetDelta = baseline == null
                ? 0f
                : Vector3.Distance(baseline.TargetPosition, current.TargetPosition);

            Plugin.Log.LogInfo(
                $"[nav reachability] event={Safe(routeEvent)} target=\"{Safe(target.Name)}\" " +
                $"kind={target.Kind} status={current.State} targetLocked={targetLocked} " +
                $"graphInstance={current.GraphInstanceId} graphRevision={graphRevision} " +
                $"playerWorld={Vector(current.PlayerPosition)} " +
                $"playerNode={Vector(current.PlayerNodePosition)} playerArea={current.PlayerArea} " +
                $"playerGraph={current.PlayerGraphIndex} playerTag={current.PlayerTag} " +
                $"playerWalkable={current.PlayerWalkable} " +
                $"targetWorld={Vector(current.TargetPosition)} " +
                $"activationAim={Vector(target.AimPosition)} " +
                $"targetNode={Vector(current.TargetNodePosition)} targetArea={current.TargetArea} " +
                $"targetGraph={current.TargetGraphIndex} targetTag={current.TargetTag} " +
                $"targetWalkable={current.TargetWalkable} nodeSize={current.NodeSize:0.00} " +
                $"playerDeltaSinceBlocked={playerDelta:0.00} " +
                $"targetDeltaSinceBlocked={targetDelta:0.00} " +
                $"unknownReason=\"{Safe(current.UnknownReason)}\" " +
                $"lastGraphCause=\"{Safe(NavigationGraphDiagnostics.LastCauseForLog)}\" " +
                $"blockers=\"{Safe(blockers?.ForLog() ?? "none")}\"");
        }

        public static void LogInteractionScan(
            Interaction interaction,
            InteractionAvailability state,
            InteractionClassification classification,
            string name,
            bool included,
            string reason)
        {
            if (!Enabled || interaction == null || state == null) return;
            var aim = interaction.transform.position + interaction.ActivatorOffset;

            Plugin.Log.LogInfo(
                $"[scan interaction] name=\"{Safe(name)}\" type={interaction.GetType().Name} " +
                $"world={Vector(interaction.transform.position)} aim={Vector(aim)} " +
                $"role={classification.Role} resource={classification.Resource} " +
                $"bulk={classification.Bulk} " +
                $"objectActive={state.ObjectActive} enabled={state.ComponentEnabled} " +
                $"registered={state.Registered} selectable={state.HasSelectableLabel} " +
                $"available={state.Available} actions={state.ActionsForLog} " +
                $"labels=\"{Labels(state)}\" included={included} " +
                $"reason=\"{Safe(reason ?? state.UnavailableReason)}\"");
        }

        public static void LogTargetCategory(
            string category,
            int scanned,
            int shown,
            int bulkResourcesScanned,
            int bulkResourcesShown)
        {
            if (!Enabled) return;

            // storyGateClosed ties an empty category directly to the game blanking every
            // label, which is the correlation that took a whole session to establish by hand.
            Plugin.Log.LogInfo(
                $"[scan category] name=\"{Safe(category)}\" scanned={scanned} shown={shown} " +
                $"bulkResourcesScanned={bulkResourcesScanned} " +
                $"bulkResourcesShown={bulkResourcesShown} " +
                $"storyGateClosed={Navigation.StoryLabelGate.Closed}");
        }

        public static void LogPendingRecruitScan(PointOfInterest point, int pendingCount)
        {
            if (!Enabled || point == null) return;

            Plugin.Log.LogInfo(
                $"[scan pending recruit] pending={pendingCount} " +
                $"name=\"{Safe(point.Name)}\" world={Vector(point.Position)} " +
                $"effectiveAvailability={point.Availability}");
        }

        public static void LogBasePassageScan(
            PointOfInterest point, InteractionAvailability interactionState)
        {
            if (!Enabled || point?.BasePassageGate == null || interactionState == null) return;

            var gate = point.BasePassageGate;
            Plugin.Log.LogInfo(
                $"[scan passage] name=\"{Safe(point.Name)}\" " +
                $"world={Vector(gate.transform.position)} aim={Vector(point.AimPosition)} " +
                $"open={BasePassage.IsOpen(gate)} effectiveAvailability={point.Availability} " +
                $"interactionAvailable={interactionState.Available} " +
                $"blockReason=\"{Safe(interactionState.FirstLabel)}\" " +
                $"returningToBase={point.IsReturnPassage} " +
                $"main={gate.IsMainDoor} dlc={gate.IsDLCDoor} woolhaven={gate.IsWoolhavenDoor} " +
                $"blockerActive={(gate.BlockingCollider != null && gate.BlockingCollider.activeSelf)}");
        }

        public static void LogDungeonDoorScan(
            PointOfInterest point, InteractionAvailability interactionState)
        {
            if (!Enabled || point?.DungeonDoor == null || interactionState == null) return;

            var door = point.DungeonDoor;
            var state = point.CurrentDungeonDoorState;
            Plugin.Log.LogInfo(
                $"[scan dungeon door] name=\"{Safe(point.Name)}\" location={door.Location} " +
                $"world={Vector(door.transform.position)} aim={Vector(point.AimPosition)} " +
                $"pathAim={Vector(point.PathPosition)} " +
                $"requiredFollowers={state.RequiredFollowers} " +
                $"eligibleFollowers={state.CurrentFollowers} " +
                $"displayedFollowers={state.DisplayedFollowers} " +
                $"hasFollowers={state.HasFollowers} unlocked={state.Unlocked} " +
                $"triggerEnabled={state.TransitionTriggerEnabled} " +
                $"triggerCenter={(door.CollideForDoor == null ? "none" : Vector(door.CollideForDoor.bounds.center))} " +
                $"blockerEnabled={state.BlockingColliderEnabled} " +
                $"effectiveAvailability={state.Availability} " +
                $"genericAvailability={interactionState.Available} " +
                $"genericActions={interactionState.ActionsForLog} " +
                $"genericLabel=\"{Safe(interactionState.FirstLabel)}\"");
        }

        public static void LogWeaponPodiumScan(
            PointOfInterest point, InteractionAvailability interactionState)
        {
            if (!Enabled || point?.WeaponPodium == null || interactionState == null) return;

            var podium = point.WeaponPodium;
            var state = WeaponPodiumTarget.Inspect(podium, interactionState);
            Plugin.Log.LogInfo(
                $"[scan weapon podium] name=\"{Safe(point.Name)}\" " +
                $"world={Vector(podium.transform.position)} type={podium.Type} " +
                $"equipment={state.EquipmentType} relic={state.RelicType} " +
                $"kind={state.ItemKind} level={state.Level} ready={state.ItemReady} " +
                $"taken={state.Taken} activated={state.Activated} " +
                $"effectiveAvailability={(state.Available ? PoiAvailability.Available : PoiAvailability.Unavailable)} " +
                $"genericAvailability={interactionState.Available} " +
                $"registered={interactionState.Registered} " +
                $"genericActions={interactionState.ActionsForLog} " +
                $"genericLabel=\"{Safe(interactionState.FirstLabel)}\"");
        }

        /// <summary>
        /// Records the physical state behind an enterable building's doorway, so the next log
        /// can distinguish "the entrance was never scanned" from "it was scanned and reported
        /// blocked" — the failure that hid the Temple entirely.
        /// </summary>
        public static void LogBuildingEntranceScan(
            EnterBuilding entrance, string name, UnityEngine.Vector3 aim)
        {
            if (!Enabled || entrance == null) return;

            var blocker = entrance.blocker;
            Plugin.Log.LogInfo(
                $"[scan building entrance] name=\"{Safe(name)}\" " +
                $"destination={entrance.Destination} " +
                $"world={Vector(entrance.transform.position)} aim={Vector(aim)} " +
                $"open={BuildingEntrance.IsOpen(entrance)} " +
                $"blockerActive={(blocker != null && blocker.activeSelf)} " +
                $"componentEnabled={entrance.enabled} " +
                $"type={entrance.GetType().Name}");
        }

        public static void LogDoorScan(Door door, string name)
        {
            if (!Enabled || door == null) return;

            var controller = door.RoomLockController;
            var controllerActive = controller != null && controller.gameObject.activeInHierarchy;
            var blockerActive = controller != null && controller.BlockingCollider != null &&
                                controller.BlockingCollider.activeInHierarchy;
            var open = controller == null || controller.Open;
            var forced = controller != null && controller.ForcedLocked;

            Plugin.Log.LogInfo(
                $"[scan door] name=\"{Safe(name)}\" direction={door.direction} " +
                $"connection={door.ConnectionType} world={Vector(door.transform.position)} " +
                $"controllerActive={controllerActive} open={open} forcedLocked={forced} " +
                $"blockerActive={blockerActive} classifiedLocked={PointOfInterest.DoorIsLocked(door)} " +
                $"classifiedUnavailable={PointOfInterest.DoorIsUnavailable(door)}");
        }

        public static void LogBeaconTargetChange(string mode, Vector3 position, Vector3? previous)
        {
            if (!Enabled) return;

            Plugin.Log.LogInfo(
                $"[beacon target] mode={mode} world={Vector(position)} " +
                $"previous={(previous.HasValue ? Vector(previous.Value) : "none")} " +
                $"jump={(previous.HasValue ? Vector3.Distance(previous.Value, position).ToString("0.00") : "n/a")}");
        }

        internal static string InteractionName(Interaction interaction)
        {
            if (interaction == null) return "none";
            var state = InteractionAvailability.Inspect(interaction);
            if (interaction is Interaction_BaseDungeonDoor dungeonDoor)
                return DungeonDoorPassage.Name(dungeonDoor, state);
            if (interaction is Interaction_WeaponSelectionPodium weaponPodium)
                return WeaponPodiumTarget.Name(weaponPodium, state);
            return state.FirstLabel ?? interaction.GetType().Name;
        }

        private static string Labels(InteractionAvailability state)
        {
            return $"primary:{Safe(state.PrimaryLabel)}; secondary:{Safe(state.SecondaryLabel)}; " +
                   $"third:{Safe(state.ThirdLabel)}; fourth:{Safe(state.FourthLabel)}";
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\r", " ").Replace("\n", " ").Replace("\"", "'");
        }

        private static string Vector(Vector3 value) =>
            $"({value.x:0.00},{value.y:0.00},{value.z:0.00})";
    }

    /// <summary>
    /// Captures the primary interaction input before the game consumes it and logs the
    /// selected interaction again afterward. This establishes whether E reached Rewired,
    /// which target the Interactor had selected, and which availability gate was false.
    /// </summary>
    [HarmonyPatch(typeof(Interactor), "Update")]
    internal static class InteractionInputDiagnostics
    {
        private sealed class InputSnapshot
        {
            public string Action;
            public string Phase;
            public Interaction Before;
            public float HoldProgress;
            public string PlayerState;
        }

        private static float _nextHeldLogAt;

        [HarmonyPrefix]
        private static void BeforeUpdate(Interactor __instance, ref InputSnapshot __state)
        {
            if (!NavigationDiagnostics.Enabled || __instance == null ||
                __instance != NavigatorPlayer.ResolveInteractor())
                return;

            try
            {
                var player = __instance.GetComponent<PlayerFarming>();
                var down = InputManager.Gameplay.GetInteractButtonDown(player);
                var up = InputManager.Gameplay.GetInteractButtonUp(player);
                var held = InputManager.Gameplay.GetInteractButtonHeld(player);
                var second = InputManager.Gameplay.GetInteract2ButtonDown(player);
                var third = InputManager.Gameplay.GetInteract3ButtonDown(player);
                var fourth = InputManager.Gameplay.GetInteract4ButtonDown(player);

                if (!down && !up && !second && !third && !fourth &&
                    (!held || Time.unscaledTime < _nextHeldLogAt))
                    return;
                if (held) _nextHeldLogAt = Time.unscaledTime + 0.5f;

                var current = __instance.CurrentInteraction;
                var state = __instance.GetComponent<StateMachine>();
                __state = new InputSnapshot
                {
                    Action = down || up || held ? "Interact" :
                        second ? "Interact 2" : third ? "Interact 3" : "Interact 4",
                    Phase = down ? "down" : up ? "up" : held ? "held" : "down",
                    Before = current,
                    HoldProgress = current == null ? 0f : current.HoldProgress,
                    PlayerState = state == null ? "none" : state.CURRENT_STATE.ToString(),
                };
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[interaction input] capture failed: {e.Message}");
            }
        }

        [HarmonyPostfix]
        private static void AfterUpdate(Interactor __instance, InputSnapshot __state)
        {
            if (__state == null || __instance == null) return;

            var beforeState = InteractionAvailability.Inspect(__state.Before);
            var dungeonDoor = __state.Before as Interaction_BaseDungeonDoor;
            var dungeonState = dungeonDoor == null
                ? null
                : DungeonDoorPassage.Inspect(dungeonDoor, beforeState);
            var effectiveAvailability = dungeonState == null
                ? (beforeState.Available ? PoiAvailability.Available : PoiAvailability.Unavailable)
                : dungeonState.Availability;
            var reason = __state.Before == null
                ? "no current interaction"
                : dungeonState != null && dungeonState.Availability == PoiAvailability.Locked
                    ? DungeonDoorText.Requirement(
                        dungeonState.CurrentFollowers, dungeonState.RequiredFollowers)
                    : beforeState.UnavailableReason;
            var tracked = Navigator.TrackedTarget;
            var holdRequired = __state.Before != null && __state.Before.HoldToInteract;
            var holdActions = false;
            try
            {
                holdActions = SettingsManager.Settings.Accessibility.HoldActions;
            }
            catch (System.Exception)
            {
                // The rest of the input evidence remains valid before settings initialise.
            }

            Plugin.Log.LogInfo(
                $"[interaction input] action=\"{__state.Action}\" phase={__state.Phase} " +
                $"playerState={__state.PlayerState} " +
                $"before=\"{NavigationDiagnostics.InteractionName(__state.Before)}\" " +
                $"after=\"{NavigationDiagnostics.InteractionName(__instance.CurrentInteraction)}\" " +
                $"availableBefore={beforeState.Available} " +
                $"effectiveAvailability={effectiveAvailability} reason=\"{reason}\" " +
                $"actions={beforeState.ActionsForLog} holdRequired={holdRequired} " +
                $"holdActionsSetting={holdActions} holdProgressBefore={__state.HoldProgress:0.00} " +
                $"holdProgressAfter={(__state.Before == null ? 0f : __state.Before.HoldProgress):0.00} " +
                $"tracked=\"{(tracked == null ? "none" : tracked.Name)}\" " +
                $"trackedIsCurrent={tracked != null && tracked.Interaction == __instance.CurrentInteraction}");
        }
    }
}
