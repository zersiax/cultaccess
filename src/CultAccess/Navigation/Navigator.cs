using CultAccess.Speech;
using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Walking assistance: pick a destination, then get told which way to push.
    ///
    /// Guidance follows a real path from the game's own A* graph rather than pointing in
    /// the straight-line direction of the target. That distinction is the entire value of
    /// this feature — a straight-line bearing happily walks you into a wall or across a
    /// river, and a blind player has no way to notice that is happening. Following the
    /// graph the game's own NPCs walk on means the route is known-walkable.
    ///
    /// The path is recomputed when the player drifts away from it, so wandering off does
    /// not require re-selecting the target.
    /// </summary>
    public static partial class Navigator
    {
        // Two checks per second is responsive to moving enemies and newly opened barriers,
        // while each check remains a pair of nearest-node lookups rather than a path request.
        private const float ReachabilityRetryInterval = 0.5f;

        private static PointOfInterest _tracked;
        private static PendingRouteState _pendingRoute;
        private static float _nextAutoAnnounce;
        private static AstarPath _hookedGraph;
        private static int _graphRevision;
        private static bool _suppressRecoveredRouteDirection;
        private static string _lastRouteBearing;
        private static bool _postPathInstructionAnnounced;
        private static float _catalogueStaleAt;
        private static object _lastRoom;

        /// <summary>
        /// How long to let a new room settle before re-scanning it.
        ///
        /// The graph is replaced the instant a room loads, which is earlier than its contents
        /// finish spawning. Scanning on that signal alone would produce a confidently empty
        /// room, which is the failure this exists to prevent rather than a new form of it.
        /// </summary>
        private const float RoomSettleSeconds = 0.75f;

        /// <summary>Seconds between automatic guidance updates while tracking.</summary>
        public static float AutoAnnounceInterval = 3f;

        /// <summary>
        /// Point the audio beacon at the destination while guiding. Derived from the
        /// wayfinding mode so there is one place to choose how guidance reaches the player.
        /// </summary>
        public static bool UseBeacon => Wayfinding.UsesBeacon;

        private static Transform Player => NavigatorPlayer.Resolve();

        internal static PointOfInterest TrackedTarget => _tracked;
        internal static Vector3? TrackedWaypoint => RouteFollower.CurrentWaypoint;
        internal static bool WaitingForRoute => _pendingRoute != null;
        internal static ReachabilitySnapshot LastReachability => _pendingRoute?.LastSnapshot;
        internal static int GraphRevision => _graphRevision;

        public static void Refresh(bool announce = true) => TargetCatalog.Refresh(announce);

        /// <summary>
        /// Note that the scanned world is out of date, to be re-scanned once it settles.
        ///
        /// A scan costs about 57 ms on this machine, so it cannot be done per keypress and the
        /// catalogue is deliberately built once and reused. The cost of that is staleness, and
        /// a session log showed exactly what staleness looks like: after a room cleared, every
        /// target in the list was a corpse, so the Everything filter reported four scanned and
        /// none shown, and the player had no targets at all until they pressed rescan by hand.
        ///
        /// Marked rather than done immediately, both to settle the room and so that several
        /// causes arriving together — barriers opening, destructibles breaking, the graph being
        /// replaced — cost one scan between them instead of one each.
        /// </summary>
        /// <param name="settle">
        /// How long to wait first. The room-change default lets contents finish spawning;
        /// a caller that already knows the thing it wants exists — combat has counted live
        /// enemies — passes zero, because there is nothing left to wait for and waiting is
        /// what let the enemies filter be announced empty.
        /// </param>
        internal static void MarkCatalogueStale(string reason, float settle = RoomSettleSeconds)
        {
            if (_catalogueStaleAt > 0f) return;

            _catalogueStaleAt = Time.unscaledTime + settle;

            // Never zero, or the "is something pending?" test above cannot tell pending from
            // idle and a burst of causes would each queue their own scan.
            if (_catalogueStaleAt <= 0f) _catalogueStaleAt = float.Epsilon;

            Plugin.Log.LogInfo($"[scan] catalogue marked stale reason={reason} settle={settle:0.00}s");
        }

        /// <summary>
        /// Notice the game changing rooms, which is the moment the whole scanned catalogue
        /// stops being about anywhere the player is.
        ///
        /// **The A* graph is not the room.** This originally hooked graph replacement, which
        /// looked right and was measured wrong: a 56-room session produced only 5
        /// `graph-replaced` events across 7 graph instances, because the game reuses one graph
        /// across a whole floor. Rooms changed underneath it and the catalogue kept describing
        /// the last one, which is what left the target list holding three dead things and
        /// every filter reporting nothing.
        ///
        /// `BiomeGenerator.Instance.CurrentRoom` is the game's own answer to "which room is
        /// this", and comparing the reference is a field read per frame.
        /// </summary>
        private static void DetectRoomChange()
        {
            object room;
            try
            {
                var generator = MMBiomeGeneration.BiomeGenerator.Instance;
                room = generator == null ? null : generator.CurrentRoom;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[scan] could not read the current room: {e.Message}");
                return;
            }

            if (ReferenceEquals(room, _lastRoom)) return;

            var first = _lastRoom == null;
            _lastRoom = room;
            if (first || room == null) return;

            MarkCatalogueStale("room-changed");
        }

        private static void RefreshStaleCatalogueWhenDue()
        {
            if (_catalogueStaleAt <= 0f || Time.unscaledTime < _catalogueStaleAt) return;

            // Cleared first, so a scan that throws cannot leave this re-entering every frame.
            _catalogueStaleAt = 0f;
            Plugin.Log.LogInfo("[scan] re-scanning after a world change");
            TargetCatalog.Refresh(announce: false, preserveSelection: true);
        }

        /// <summary>
        /// Rebuild a cached catalogue after a world mutation without interrupting speech or
        /// moving the user's selection when that same target still exists.
        /// </summary>
        internal static void RefreshAfterWorldChange() =>
            TargetCatalog.Refresh(announce: false, preserveSelection: true);

        public static void CycleCategory(int direction = 1) =>
            TargetCatalog.CycleCategory(direction);

        public static void Cycle(int direction) => TargetCatalog.Cycle(direction);

        public static void AnnounceCategory() => TargetCatalog.AnnounceCategory();

        /// <summary>Start guiding to the currently selected target.</summary>
        public static void TrackSelected()
        {
            var target = TargetCatalog.Selected;
            if (target == null)
            {
                Refresh();
                return;
            }

            var player = Player;
            if (player == null)
            {
                Speaker.Say("That target is gone.");
                return;
            }

            // A dead selection almost always means the catalogue is describing a room that has
            // moved on, and refusing was a dead end: the player pressed guide, heard "that
            // target is gone", and was left holding the same stale list to try again from.
            // Re-scan and hand back a live selection instead, which is what Cycle already does.
            if (!target.Alive)
            {
                Plugin.Log.LogInfo(
                    $"[scan] guidance target \"{target.Name}\" is gone; re-scanning rather than refusing");
                Refresh();
                return;
            }

            var locked = target.Availability == PoiAvailability.Locked;
            if (target.DungeonDoor != null &&
                target.Availability != PoiAvailability.Available)
            {
                // Follower gates and scripted blocker state are stable, explicit reasons.
                // Guiding to the inaccessible side only sends the player around the hub.
                Speaker.Say($"{target.Describe()}. Guidance not started.");
                return;
            }

            var controller = target.Door == null ? null : target.Door.RoomLockController;
            if (locked && controller != null && controller.ForcedLocked)
            {
                // ForcedLocked is the game's explicit non-transient lock authority. Ordinary
                // combat-room locks are retained below because DoorDown can open them later.
                Speaker.Say($"{target.Name} is locked. Guidance not started.");
                return;
            }

            EnsureGraphHook();
            var reachability = RouteFollower.InspectReachability(
                player.position, target.PathPosition);

            if (locked || reachability.State == RouteReachabilityState.Disconnected)
            {
                BeginPendingRoute(
                    target, player, reachability, locked, "preflight", hadActiveRoute: false);
                return;
            }

            Diagnostics.NavigationDiagnostics.LogReachability(
                "preflight", target, reachability, null, null, false, _graphRevision);
            StartRoute(target, player, recovered: false);
            Speaker.Say(target.Availability == PoiAvailability.Unavailable
                ? $"Guiding to the location of {target.Name}. It is currently unavailable."
                : $"Guiding to {target.Name}.");
        }

        /// <summary>One key to start guiding and to stop it again.</summary>
        public static void ToggleTracking()
        {
            if (_tracked != null) StopTracking();
            else TrackSelected();
        }

        public static void StopTracking(bool announce = true)
        {
            if (_tracked == null)
            {
                if (announce) Speaker.Say("Not guiding.");
                return;
            }

            // Autowalk rides on guidance and cannot outlive it. Silent, because every route
            // ending — arrival, a target that is gone, the player's own stop key — already
            // says what happened in its own words.
            Autowalk.Disengage("guidance stopped");

            _tracked = null;
            _pendingRoute = null;
            _suppressRecoveredRouteDirection = false;
            _lastRouteBearing = null;
            _postPathInstructionAnnounced = false;
            RouteFollower.Stop();

            if (UseBeacon) Audio.Beacon.ClearNavigation();

            if (announce) Speaker.Say("Guidance stopped.");
        }

        internal static void Shutdown()
        {
            AstarPath.OnGraphsUpdated -= OnGraphsUpdated;
            _hookedGraph = null;
            DungeonDoorPassage.Shutdown();
            WeaponPodiumTarget.Shutdown();
            StopTracking(announce: false);
        }

        /// <summary>Speak the current guidance on demand.</summary>
        public static void AnnounceGuidance()
        {
            if (_tracked == null)
            {
                TargetCatalog.AnnounceSelected();
                return;
            }

            var player = Player;
            if (player == null) return;

            if (!_tracked.Alive)
            {
                Speaker.Say("Target is gone.");
                StopTracking(announce: false);
                return;
            }

            var remaining = RoutePlanarMath.Distance(
                player.position.x, player.position.y,
                _tracked.AimPosition.x, _tracked.AimPosition.y);

            if (_pendingRoute != null)
            {
                TryResumePendingRoute(player, "on-demand");
                if (_pendingRoute == null) return;

                var blocker = _pendingRoute.Blockers?.Best;
                var detail = blocker == null ? string.Empty : $" {blocker.Speak(player.position)}";
                var state = _tracked.Availability == PoiAvailability.Locked
                    ? "waiting for its room barrier to open"
                    : "no current route; still checking";
                Speaker.Say($"{_tracked.Name}, {Compass.DescribeDistance(remaining)}, {state}.{detail}");
                return;
            }

            if (RouteFollower.AwaitingPath && !RouteFollower.CurrentWaypoint.HasValue)
            {
                Speaker.Say($"{_tracked.Name}, {Compass.DescribeDistance(remaining)}, finding route.");
                return;
            }

            var step = RouteFollower.CurrentWaypoint;
            if (!step.HasValue)
            {
                // No route available: fall back to the straight line, and say so, because
                // an unqualified bearing implies a walkable route that we have not verified.
                if (Compass.TryDescribe(player.position, _tracked.AimPosition, out var direct, out _))
                {
                    _lastRouteBearing = direct;
                    Speaker.Say(
                        RouteGuidanceText.DirectLine(
                            direct, _tracked.Name, Compass.DescribeTravelDistance(remaining),
                            BlockedAlong(player.position, _tracked.AimPosition)),
                        SpeechPriority.Superseding);
                }
                return;
            }

            if (!TrySpeakWaypointInstruction(player, onlyChangedHeading: false))
            {
                Speaker.Say(
                    $"{_tracked.Name}, {Compass.DescribeDistance(remaining)}.",
                    SpeechPriority.Superseding);
            }
        }

        /// <summary>
        /// Speak a complete movement instruction for the live route point. During a waypoint
        /// transition, straight-through graph points remain silent and only an actionable
        /// heading change interrupts speech.
        /// </summary>
        private static bool TrySpeakWaypointInstruction(
            Transform player,
            bool onlyChangedHeading)
        {
            if (player == null || _tracked == null) return false;

            var step = RouteFollower.CurrentWaypoint;
            if (!step.HasValue ||
                !Compass.TryDescribe(player.position, step.Value, out var bearing, out var stepDistance))
                return false;

            var firstInstruction = string.IsNullOrEmpty(_lastRouteBearing);
            var changedHeading = !firstInstruction && _lastRouteBearing != bearing;
            if (onlyChangedHeading && !firstInstruction && !changedHeading) return false;

            var aim = _tracked.AimPosition;
            var remaining = RoutePlanarMath.Distance(
                player.position.x, player.position.y, aim.x, aim.y);
            var finalStep = !_tracked.HasPostPathApproach &&
                            RouteFollower.IsOnFinalWaypoint;
            var message = RouteGuidanceText.Step(
                bearing,
                Compass.DescribeTravelDistance(stepDistance),
                _tracked.Name,
                Compass.DescribeDistance(remaining),
                // A fresh open dungeon door first routes to the reachable threshold;
                // the transition-trigger centre is a separate final direct approach.
                finalStep,
                changedHeading,
                firstInstruction,
                BlockedAlong(player.position, step.Value));

            _lastRouteBearing = bearing;
            Speaker.Say(message, SpeechPriority.Superseding);
            if (Diagnostics.NavigationDiagnostics.Enabled)
            {
                Plugin.Log.LogInfo(
                    $"[nav instruction] kind={(changedHeading ? "turn" : firstInstruction ? "go" : "continue")} " +
                    $"bearing={bearing} stepDistance={stepDistance:0.00} remaining={remaining:0.00} " +
                    $"finalStep={finalStep} blocked={BlockedAlong(player.position, step.Value)} " +
                    $"target=\"{_tracked.Name}\"");
            }
            return true;
        }

        /// <summary>
        /// True when the wall sonar is in contact roughly the way the instruction points.
        ///
        /// Sixty degrees either side, because the instruction is quantised to eight compass
        /// points and the sonar probes the live movement direction: the two are describing the
        /// same intent through different resolutions, and a tighter cone would miss exactly
        /// the near-miss cases this exists for.
        /// </summary>
        private static bool BlockedAlong(Vector3 from, Vector3 towards)
        {
            if (!Combat.ObstacleSonar.Blocked) return false;

            var heading = new Vector2(towards.x - from.x, towards.y - from.y);
            if (heading.sqrMagnitude <= 0.0001f) return false;

            const float sixtyDegrees = 0.5f;
            return Vector2.Dot(
                heading.normalized, Combat.ObstacleSonar.BlockedDirection) >= sixtyDegrees;
        }

        /// <summary>
        /// Open base dungeon doors have a short, intentional off-graph continuation from
        /// their reachable threshold into the transition collider. Announce that handoff
        /// immediately so speech-only navigation never becomes step-and-wait at the door.
        /// </summary>
        private static bool TrySpeakPostPathInstruction(Transform player)
        {
            if (_postPathInstructionAnnounced || player == null || _tracked == null ||
                !_tracked.HasPostPathApproach || RouteFollower.CurrentWaypoint.HasValue)
                return false;

            var aim = _tracked.AimPosition;
            if (!Compass.TryDescribe(player.position, aim, out var bearing, out _)) return false;

            var remaining = RoutePlanarMath.Distance(
                player.position.x, player.position.y, aim.x, aim.y);
            _postPathInstructionAnnounced = true;
            _lastRouteBearing = bearing;
            Speaker.Say(
                RouteGuidanceText.DirectLine(
                    bearing, _tracked.Name, Compass.DescribeTravelDistance(remaining),
                    BlockedAlong(player.position, _tracked.AimPosition)),
                SpeechPriority.Superseding);

            if (Diagnostics.NavigationDiagnostics.Enabled)
            {
                Plugin.Log.LogInfo(
                    $"[nav instruction] kind=direct bearing={bearing} " +
                    $"stepDistance={remaining:0.00} remaining={remaining:0.00} " +
                    $"finalStep=True target=\"{_tracked.Name}\"");
            }
            return true;
        }

        /// <summary>Driven from the plugin's Update. Cheap when not tracking.</summary>
        public static void Tick()
        {
            // Above the guidance check on purpose. Room changes have to be noticed whether or
            // not something is being tracked; keeping this behind the early return is why a
            // new room's catalogue stayed empty until the player rescanned by hand.
            EnsureGraphHook();
            DetectRoomChange();
            RefreshStaleCatalogueWhenDue();

            if (_tracked == null) return;

            var player = Player;
            if (player == null) return;

            if (!_tracked.Alive)
            {
                Speaker.Say("Target is gone.");
                StopTracking(announce: false);
                return;
            }

            var position = player.position;

            if (_pendingRoute != null)
            {
                if (_tracked.HasArrived(player, NavigatorPlayer.ResolveInteractor()))
                {
                    Speaker.Say(_tracked.ArrivalAnnouncement(), SpeechPriority.Queued);
                    StopTracking(announce: false);
                    return;
                }

                if (_pendingRoute.RetryRequested ||
                    Time.unscaledTime >= _pendingRoute.NextRetryAt)
                {
                    var trigger = _pendingRoute.RetryRequested
                        ? _pendingRoute.RetryTrigger ?? "world-change"
                        : "periodic";
                    TryResumePendingRoute(player, trigger);
                }
                return;
            }

            if (_tracked.Availability == PoiAvailability.Locked)
            {
                var snapshot = RouteFollower.InspectReachability(position, _tracked.PathPosition);
                BeginPendingRoute(
                    _tracked, player, snapshot, locked: true,
                    trigger: "target-locked", hadActiveRoute: true);
                return;
            }

            RouteFollower.Tick(position, _tracked.PathPosition);
            if (_pendingRoute != null) return;

            if (_tracked.HasArrived(player, NavigatorPlayer.ResolveInteractor()))
            {
                // Queued: arriving nearly always coincides with the interaction prompt for
                // whatever you arrived at, and interrupting would swallow the more useful
                // of the two messages.
                Speaker.Say(_tracked.ArrivalAnnouncement(), SpeechPriority.Queued);
                StopTracking(announce: false);
                return;
            }

            if (Time.unscaledTime < _nextAutoAnnounce) return;
            _nextAutoAnnounce = Time.unscaledTime + AutoAnnounceInterval;

            // Beacon-only guidance still runs the whole route machine, including arrival and
            // re-routing; it just does not narrate. The timer is advanced either way so that
            // switching mode mid-route does not produce a burst of backdated instructions.
            if (Wayfinding.SpeaksAutomatically) AnnounceGuidance();
        }

        private static void UpdateBeaconTarget()
        {
            if (!UseBeacon || _tracked == null) return;

            var waypoint = RouteFollower.CurrentWaypoint;
            if (waypoint.HasValue)
            {
                Audio.Beacon.SetNavigationPosition(waypoint.Value);
                return;
            }

            var offset = _tracked.AimPosition - _tracked.Transform.position;
            Audio.Beacon.SetNavigationTarget(_tracked.Transform, offset);
        }

    }
}
