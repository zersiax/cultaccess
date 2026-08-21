using System;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Owns A* requests and advancement through their waypoints. Selection and spoken
    /// guidance remain in <see cref="Navigator"/>; this class reports route changes through
    /// callbacks so the beacon always follows the same waypoint as speech.
    /// </summary>
    internal static class RouteFollower
    {
        private const float PathTimeout = 2.5f;
        private const float WaypointDistance = 1.25f;
        private const float RerouteDistance = 4f;

        private static List<Vector3> _waypoints;
        private static int _waypointIndex;
        private static Seeker _seeker;
        private static AstarPath _seekerGraph;
        private static bool _awaitingPath;
        private static float _pathRequestedAt;
        private static Vector3 _pathComputedFrom;
        private static Vector3 _pathComputedTo;
        private static bool _pathExhausted;
        private static Vector3 _pathExhaustedAt;
        private static Vector3 _pathExhaustedTarget;
        private static bool _receivedFirstPath;
        private static float _waypointDepth;
        private static int _requestVersion;
        private static Action<bool> _routeChanged;
        private static Action<string> _routeFailed;

        public static bool AwaitingPath => _awaitingPath;

        public static bool IsOnFinalWaypoint =>
            _waypoints != null && _waypointIndex >= 0 &&
            _waypointIndex == _waypoints.Count - 1;

        public static Vector3? CurrentWaypoint
        {
            get
            {
                if (_waypoints == null || _waypointIndex >= _waypoints.Count) return null;
                // A* stores this XY game's graph nodes at Z=0, while characters commonly
                // render at another Z depth (DoorRoom's Lamb is at -1.23). Projecting the
                // node onto the Lamb's current depth prevents camera projection from turning
                // a northward walking step into a southward screen bearing.
                var waypoint = _waypoints[_waypointIndex];
                waypoint.z = _waypointDepth;
                return waypoint;
            }
        }

        public static void Start(
            Vector3 from,
            Vector3 to,
            Action<bool> routeChanged,
            Action<string> routeFailed)
        {
            Stop();
            _waypointDepth = from.z;
            _routeChanged = routeChanged;
            _routeFailed = routeFailed;
            RequestPath(from, to);
        }

        public static void Stop()
        {
            _requestVersion++;
            _awaitingPath = false;
            _waypoints = null;
            _waypointIndex = 0;
            _pathExhausted = false;
            _receivedFirstPath = false;
            _routeChanged = null;
            _routeFailed = null;
        }

        public static void Tick(Vector3 position, Vector3 target)
        {
            _waypointDepth = position.z;
            if (_awaitingPath && Time.unscaledTime - _pathRequestedAt > PathTimeout)
            {
                _awaitingPath = false;
                _requestVersion++;
                _waypoints = null;
                _waypointIndex = 0;
                _pathExhausted = true;
                _pathExhaustedAt = position;
                _pathExhaustedTarget = target;
                Plugin.Log.LogInfo("Path request timed out; returning guidance to pending-route state.");
                _routeFailed?.Invoke("Path request timed out.");
                return;
            }

            AdvanceWaypoints(position, target);
        }

        /// <summary>Inspect the exact graph nodes used by the preflight route gate.</summary>
        public static ReachabilitySnapshot InspectReachability(Vector3 from, Vector3 to) =>
            ReachabilitySnapshot.Inspect(from, to);

        private static void AdvanceWaypoints(Vector3 position, Vector3 target)
        {
            if (_pathExhausted)
            {
                // Re-requesting unchanged endpoints every frame caused rapid-fire speech
                // and beacon resets. Either endpoint moving far enough is real evidence
                // that a path to a moving enemy can now differ.
                if (PlanarDistance(position, _pathExhaustedAt) > RerouteDistance ||
                    PlanarDistance(target, _pathExhaustedTarget) > RerouteDistance)
                {
                    _pathExhausted = false;
                    RequestPath(position, target);
                }
                return;
            }

            if (_waypoints == null || _waypoints.Count == 0) return;

            var previousWaypoint = _waypointIndex;
            while (_waypointIndex < _waypoints.Count &&
                   PlanarDistance(position, _waypoints[_waypointIndex]) <= WaypointDistance)
                _waypointIndex++;

            if (_waypointIndex != previousWaypoint) _routeChanged?.Invoke(false);

            if (_waypointIndex >= _waypoints.Count)
            {
                _waypoints = null;
                _waypointIndex = 0;
                _pathExhausted = true;
                _pathExhaustedAt = position;
                _pathExhaustedTarget = target;
                _routeChanged?.Invoke(false);
                Plugin.Log.LogInfo(
                    $"Navigation route exhausted at {Format(position)}; holding direct guidance " +
                    "until the player or target moves away from the computed endpoint.");
                return;
            }

            if (PlanarDistance(target, _pathComputedTo) > RerouteDistance ||
                PlanarDistance(position, _waypoints[_waypointIndex]) > RerouteDistance &&
                PlanarDistance(position, _pathComputedFrom) > RerouteDistance)
                RequestPath(position, target);
        }

        private static void RequestPath(Vector3 from, Vector3 to)
        {
            if (_awaitingPath || AstarPath.active == null) return;

            var seeker = EnsureSeeker();
            if (seeker == null) return;

            _pathComputedFrom = from;
            _pathComputedTo = to;
            _awaitingPath = true;
            _pathRequestedAt = Time.unscaledTime;
            var requestVersion = ++_requestVersion;

            try
            {
                seeker.StartPath(from, to, path => OnPathComplete(path, requestVersion));
            }
            catch (Exception e)
            {
                _awaitingPath = false;
                Plugin.Log.LogWarning($"Path request failed: {e.Message}");
                _routeFailed?.Invoke("Path request failed.");
            }
        }

        private static void OnPathComplete(Path path, int requestVersion)
        {
            if (requestVersion != _requestVersion || _routeChanged == null) return;
            _awaitingPath = false;

            if (path == null || path.error)
            {
                Plugin.Log.LogInfo($"No route found: {path?.errorLog}");
                _waypoints = null;
                _waypointIndex = 0;
                _pathExhausted = true;
                _pathExhaustedAt = _pathComputedFrom;
                _pathExhaustedTarget = _pathComputedTo;
                _routeFailed?.Invoke("No route found.");
                return;
            }

            _waypoints = new List<Vector3>(path.vectorPath);
            _waypointIndex = _waypoints.Count > 1 ? 1 : 0;
            _pathExhausted = false;

            var first = !_receivedFirstPath;
            _receivedFirstPath = true;
            _routeChanged(first);
        }

        /// <summary>
        /// A Seeker must live on a GameObject. AstarPath is rebuilt between rooms, so the
        /// hidden Seeker is rebuilt whenever the active graph instance changes too.
        /// </summary>
        private static Seeker EnsureSeeker()
        {
            if (_seeker != null && !ReferenceEquals(_seekerGraph, AstarPath.active))
            {
                Plugin.Log.LogInfo("AstarPath was replaced; rebuilding the navigation Seeker.");
                if (_seeker.gameObject != null) UnityEngine.Object.Destroy(_seeker.gameObject);
                _seeker = null;
            }

            if (_seeker != null) return _seeker;

            _seekerGraph = AstarPath.active;
            var host = new GameObject("CultAccess Navigator");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            return _seeker = host.AddComponent<Seeker>();
        }

        private static string Format(Vector3 value) =>
            $"({value.x:0.00},{value.y:0.00},{value.z:0.00})";

        private static float PlanarDistance(Vector3 from, Vector3 to) =>
            RoutePlanarMath.Distance(from.x, from.y, to.x, to.y);
    }
}
