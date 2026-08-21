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
            if (player == null || !target.Alive)
            {
                Speaker.Say("That target is gone.");
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
                            direct, _tracked.Name, Compass.DescribeTravelDistance(remaining)),
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
                firstInstruction);

            _lastRouteBearing = bearing;
            Speaker.Say(message, SpeechPriority.Superseding);
            if (Diagnostics.NavigationDiagnostics.Enabled)
            {
                Plugin.Log.LogInfo(
                    $"[nav instruction] kind={(changedHeading ? "turn" : firstInstruction ? "go" : "continue")} " +
                    $"bearing={bearing} stepDistance={stepDistance:0.00} remaining={remaining:0.00} " +
                    $"finalStep={finalStep} target=\"{_tracked.Name}\"");
            }
            return true;
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
                    bearing, _tracked.Name, Compass.DescribeTravelDistance(remaining)),
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
            if (_tracked == null) return;

            EnsureGraphHook();

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
