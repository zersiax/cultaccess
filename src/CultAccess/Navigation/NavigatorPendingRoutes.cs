using CultAccess.Speech;
using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>Temporary reachability, graph-change observation, and route recovery.</summary>
    public static partial class Navigator
    {
        private static void StartRoute(
            PointOfInterest target,
            Transform player,
            bool recovered)
        {
            _tracked = target;
            _pendingRoute = null;
            _lastRouteBearing = null;
            _postPathInstructionAnnounced = false;
            _nextAutoAnnounce = recovered
                ? Time.unscaledTime + AutoAnnounceInterval
                : 0f;
            _suppressRecoveredRouteDirection = recovered;
            UpdateBeaconTarget();
            RouteFollower.Start(
                player.position, target.PathPosition, OnRouteChanged, OnRouteFailed);
        }

        private static void BeginPendingRoute(
            PointOfInterest target,
            Transform player,
            ReachabilitySnapshot snapshot,
            bool locked,
            string trigger,
            bool hadActiveRoute)
        {
            _tracked = target;
            RouteFollower.Stop();
            if (hadActiveRoute && UseBeacon) Audio.Beacon.ClearNavigation();

            var blockers = RouteBlockerInspector.Inspect(snapshot, target);
            _pendingRoute = new PendingRouteState
            {
                InitialSnapshot = snapshot,
                LastSnapshot = snapshot,
                Blockers = blockers,
                NextRetryAt = Time.unscaledTime + ReachabilityRetryInterval,
            };

            EnsureGraphHook();
            Diagnostics.NavigationDiagnostics.LogReachability(
                trigger, target, snapshot, snapshot, blockers, locked, _graphRevision);
            Speaker.Say(PendingRouteAnnouncement(target, player.position, blockers, locked));
        }

        private static bool TryResumePendingRoute(Transform player, string trigger)
        {
            var pending = _pendingRoute;
            var target = _tracked;
            if (pending == null || target == null) return false;

            pending.RetryRequested = false;
            pending.RetryTrigger = null;

            var locked = target.Availability == PoiAvailability.Locked;
            var snapshot = RouteFollower.InspectReachability(
                player.position, target.PathPosition);
            var blockers = RouteBlockerInspector.Inspect(snapshot, target);
            var topologyChanged = snapshot.TopologyDiffersFrom(pending.LastSnapshot);
            var blockerChanged = BlockerChanged(pending.Blockers?.Best, blockers.Best);

            pending.LastSnapshot = snapshot;
            pending.Blockers = blockers;
            pending.NextRetryAt = Time.unscaledTime + ReachabilityRetryInterval;

            if (!locked && snapshot.State == RouteReachabilityState.Reachable)
            {
                Diagnostics.NavigationDiagnostics.LogReachability(
                    "route-opened:" + trigger, target, snapshot, pending.InitialSnapshot,
                    blockers, false, _graphRevision);
                StartRoute(target, player, recovered: true);
                Speaker.Say($"Route opened. Guiding to {target.Name}.");
                return true;
            }

            if (trigger != "periodic" || topologyChanged || blockerChanged)
            {
                Diagnostics.NavigationDiagnostics.LogReachability(
                    "retry:" + trigger, target, snapshot, pending.InitialSnapshot,
                    blockers, locked, _graphRevision);
            }

            return false;
        }

        private static string PendingRouteAnnouncement(
            PointOfInterest target,
            Vector3 playerPosition,
            RouteBlockerInspection blockers,
            bool locked)
        {
            var best = blockers?.Best;
            if (locked && best != null && best.IsTargetDoorBarrier)
                return $"{target.Name} is locked; its active room barrier is closed. " +
                       "Guidance will start when it opens.";

            var start = locked
                ? $"{target.Name} is currently locked."
                : $"No current route to {target.Name}.";
            var evidence = best == null ? string.Empty : $" {best.Speak(playerPosition)}";
            return $"{start}{evidence} I will keep checking.";
        }

        private static bool BlockerChanged(
            RouteBlockerEvidence previous,
            RouteBlockerEvidence current)
        {
            if (previous == null || current == null) return previous != current;
            return previous.InstanceId != current.InstanceId ||
                   previous.Kind != current.Kind ||
                   previous.SeparatesEndpointAreas != current.SeparatesEndpointAreas;
        }

        private static void EnsureGraphHook()
        {
            var active = AstarPath.active;
            if (object.ReferenceEquals(active, _hookedGraph)) return;

            var replacingExisting = !object.ReferenceEquals(_hookedGraph, null);
            AstarPath.OnGraphsUpdated -= OnGraphsUpdated;
            _hookedGraph = active;
            if (active != null) AstarPath.OnGraphsUpdated += OnGraphsUpdated;

            if (!replacingExisting) return;

            _graphRevision++;
            Diagnostics.NavigationGraphDiagnostics.LogGraphLifecycle(
                "graph-replaced", _graphRevision, active);
            RequestPendingRetry("graph-replaced");
        }

        private static void OnGraphsUpdated(AstarPath graph)
        {
            _graphRevision++;
            Diagnostics.NavigationGraphDiagnostics.LogGraphLifecycle(
                "graphs-updated", _graphRevision, graph);
            RequestPendingRetry("graph-update");
        }

        private static void RequestPendingRetry(string trigger)
        {
            if (_pendingRoute == null) return;
            _pendingRoute.RetryRequested = true;
            _pendingRoute.RetryTrigger = trigger;
        }

        private static void OnRouteChanged(bool initialRoute)
        {
            UpdateBeaconTarget();
            if (RouteFollower.CurrentWaypoint.HasValue)
                _postPathInstructionAnnounced = false;
            else if (TrySpeakPostPathInstruction(Player))
            {
                _nextAutoAnnounce = Time.unscaledTime + AutoAnnounceInterval;
                return;
            }

            if (initialRoute)
            {
                _nextAutoAnnounce = _suppressRecoveredRouteDirection
                    ? Time.unscaledTime + AutoAnnounceInterval
                    : 0f;
                _suppressRecoveredRouteDirection = false;
                return;
            }

            // The beacon moves to every graph point immediately. Speech interrupts only
            // when that produces a different actionable heading; collinear points are an
            // implementation detail and must not sound like repeated turns.
            if (string.IsNullOrEmpty(_lastRouteBearing) &&
                Time.unscaledTime < _nextAutoAnnounce)
                return;

            if (!Wayfinding.SpeaksAutomatically) return;

            if (TrySpeakWaypointInstruction(Player, onlyChangedHeading: true))
                _nextAutoAnnounce = Time.unscaledTime + AutoAnnounceInterval;
        }

        private static void OnRouteFailed(string message)
        {
            if (_tracked == null) return;
            var player = Player;
            if (player == null)
            {
                Speaker.Say(message);
                return;
            }

            Plugin.Log.LogInfo(
                $"Navigation path request returned to pending state: {message}");
            var snapshot = RouteFollower.InspectReachability(
                player.position, _tracked.PathPosition);
            BeginPendingRoute(
                _tracked, player, snapshot,
                _tracked.Availability == PoiAvailability.Locked,
                "path-failure", hadActiveRoute: true);
        }
    }
}
