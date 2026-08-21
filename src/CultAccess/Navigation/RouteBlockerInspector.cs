using System;
using System.Collections.Generic;
using UnityEngine;

namespace CultAccess.Navigation
{
    internal static class RouteBlockerInspector
    {
        public static RouteBlockerInspection Inspect(
            ReachabilitySnapshot route,
            PointOfInterest target)
        {
            var result = new RouteBlockerInspection();
            if (route == null || target == null) return result;

            var seen = new HashSet<int>();
            AddTargetDoorBarrier(route, target, result, seen);

            try
            {
                foreach (var hit in Physics2D.LinecastAll(route.PlayerPosition, route.TargetPosition))
                {
                    var collider = hit.collider;
                    if (collider == null || !collider.enabled ||
                        !collider.gameObject.activeInHierarchy)
                        continue;

                    var evidence = Classify(collider, target);
                    if (evidence == null || !seen.Add(evidence.InstanceId)) continue;
                    PopulateAreaEvidence(route, evidence);
                    result.Candidates.Add(evidence);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Route blocker inspection failed: {e.Message}");
            }

            return result;
        }

        private static void AddTargetDoorBarrier(
            ReachabilitySnapshot route,
            PointOfInterest target,
            RouteBlockerInspection result,
            HashSet<int> seen)
        {
            var controller = target.Door == null ? null : target.Door.RoomLockController;
            if (controller == null || !controller.gameObject.activeInHierarchy || controller.Open)
                return;

            var collider = FirstEnabledCollider(controller.BlockingCollider);
            var evidence = new RouteBlockerEvidence
            {
                Kind = RouteBlockerKind.ClosedRoomBarrier,
                Source = controller,
                Collider = collider,
                Position = collider == null
                    ? controller.BlockingCollider == null
                        ? controller.transform.position
                        : controller.BlockingCollider.transform.position
                    : collider.bounds.center,
                IsTargetDoorBarrier = true,
            };

            seen.Add(evidence.InstanceId);
            PopulateAreaEvidence(route, evidence);
            result.Candidates.Add(evidence);
        }

        private static RouteBlockerEvidence Classify(Collider2D collider, PointOfInterest target)
        {
            var roomBarrier = collider.GetComponentInParent<RoomLockController>();
            if (roomBarrier != null && !roomBarrier.Open &&
                IsInside(collider.transform, roomBarrier.BlockingCollider))
            {
                return NewEvidence(RouteBlockerKind.ClosedRoomBarrier, roomBarrier, collider);
            }

            var movingBarrier = collider.GetComponentInParent<BlockingDoor>();
            if (movingBarrier != null)
                return NewEvidence(RouteBlockerKind.ClosedBarrier, movingBarrier, collider);

            var spiderNest = collider.GetComponentInParent<BreakableSpiderNest>();
            if (spiderNest != null && !BelongsToTarget(spiderNest.transform, target.Transform))
                return NewEvidence(RouteBlockerKind.DestructibleSpiderNest, spiderNest, collider);

            var breakable = collider.GetComponentInParent<BreakableTile>();
            if (breakable != null && !BelongsToTarget(breakable.transform, target.Transform))
                return NewEvidence(RouteBlockerKind.DestructibleBarrier, breakable, collider);

            return null;
        }

        private static RouteBlockerEvidence NewEvidence(
            RouteBlockerKind kind,
            Component source,
            Collider2D collider)
        {
            return new RouteBlockerEvidence
            {
                Kind = kind,
                Source = source,
                Collider = collider,
                Position = collider.bounds.center,
            };
        }

        private static void PopulateAreaEvidence(
            ReachabilitySnapshot route,
            RouteBlockerEvidence evidence)
        {
            if (route.State != RouteReachabilityState.Disconnected || evidence.Collider == null ||
                AstarPath.active == null)
                return;

            var margin = Mathf.Max(route.NodeSize, Physics2D.defaultContactOffset);
            var bounds = evidence.Collider.bounds;
            var z = (route.PlayerPosition.z + route.TargetPosition.z) * 0.5f;
            var left = Sample(new Vector3(bounds.min.x - margin, bounds.center.y, z));
            var right = Sample(new Vector3(bounds.max.x + margin, bounds.center.y, z));
            var below = Sample(new Vector3(bounds.center.x, bounds.min.y - margin, z));
            var above = Sample(new Vector3(bounds.center.x, bounds.max.y + margin, z));

            evidence.SeparatesEndpointAreas =
                OppositeEndpointAreas(left, right, route) ||
                OppositeEndpointAreas(below, above, route);
            evidence.AreaSamples =
                $"left:{left};right:{right};below:{below};above:{above};margin:{margin:0.00}";
        }

        private static NodeSample Sample(Vector3 position)
        {
            try
            {
                var nearest = AstarPath.active.GetNearest(position);
                if (nearest.node == null) return new NodeSample(position);
                return new NodeSample(position, nearest.position, nearest.node.Area, nearest.node.Walkable);
            }
            catch (Exception e)
            {
                return new NodeSample(position, e.Message);
            }
        }

        private static bool OppositeEndpointAreas(
            NodeSample first,
            NodeSample second,
            ReachabilitySnapshot route)
        {
            if (!first.Valid || !second.Valid || !first.Walkable || !second.Walkable) return false;
            return first.Area == route.PlayerArea && second.Area == route.TargetArea ||
                   first.Area == route.TargetArea && second.Area == route.PlayerArea;
        }

        private static Collider2D FirstEnabledCollider(GameObject root)
        {
            if (root == null || !root.activeInHierarchy) return null;
            foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
            {
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                    return collider;
            }
            return null;
        }

        private static bool IsInside(Transform candidate, GameObject root)
        {
            if (candidate == null || root == null) return false;
            return candidate == root.transform || candidate.IsChildOf(root.transform);
        }

        private static bool BelongsToTarget(Transform candidate, Transform target)
        {
            if (candidate == null || target == null) return false;
            return candidate == target || candidate.IsChildOf(target) || target.IsChildOf(candidate);
        }

        private sealed class NodeSample
        {
            private readonly Vector3 _requested;
            private readonly Vector3 _nearest;
            private readonly string _error;

            public readonly bool Valid;
            public readonly uint Area;
            public readonly bool Walkable;

            public NodeSample(Vector3 requested)
            {
                _requested = requested;
            }

            public NodeSample(Vector3 requested, string error)
            {
                _requested = requested;
                _error = error;
            }

            public NodeSample(Vector3 requested, Vector3 nearest, uint area, bool walkable)
            {
                _requested = requested;
                _nearest = nearest;
                Area = area;
                Walkable = walkable;
                Valid = true;
            }

            public override string ToString()
            {
                if (!Valid)
                    return string.IsNullOrEmpty(_error) ? "missing" : $"error-{Safe(_error)}";
                return $"area-{Area}-walkable-{Walkable}-request-{Format(_requested)}-node-{Format(_nearest)}";
            }

            private static string Safe(string value) =>
                value.Replace(";", ",").Replace("|", ",").Replace("\r", " ").Replace("\n", " ");

            private static string Format(Vector3 value) =>
                $"({value.x:0.00},{value.y:0.00},{value.z:0.00})";
        }
    }
}
