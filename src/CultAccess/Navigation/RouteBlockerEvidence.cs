using System.Collections.Generic;
using UnityEngine;

namespace CultAccess.Navigation
{
    internal enum RouteBlockerKind
    {
        ClosedRoomBarrier,
        ClosedBarrier,
        DestructibleBarrier,
        DestructibleSpiderNest,
    }

    /// <summary>
    /// Evidence about a known live collider on the direct line between disconnected graph
    /// endpoints. The wording deliberately distinguishes a verified object state from the
    /// inference that it is responsible for the disconnected areas.
    /// </summary>
    internal sealed class RouteBlockerEvidence
    {
        public RouteBlockerKind Kind;
        public Component Source;
        public Collider2D Collider;
        public Vector3 Position;
        public bool IsTargetDoorBarrier;
        public bool SeparatesEndpointAreas;
        public string AreaSamples;

        public int InstanceId => Source == null ? 0 : Source.GetInstanceID();

        public bool IsDestructible =>
            Kind == RouteBlockerKind.DestructibleBarrier ||
            Kind == RouteBlockerKind.DestructibleSpiderNest;

        public string Speak(Vector3 playerPosition)
        {
            var where = Location(playerPosition);
            switch (Kind)
            {
                case RouteBlockerKind.ClosedRoomBarrier:
                    if (IsTargetDoorBarrier)
                        return $"The target exit's room barrier is closed, {where}.";
                    return $"A closed room barrier is on the direct line to the target, {where}.";

                case RouteBlockerKind.ClosedBarrier:
                    return $"A closed moving barrier is on the direct line to the target, {where}.";

                case RouteBlockerKind.DestructibleSpiderNest:
                    return SeparatesEndpointAreas
                        ? $"A destructible spider nest appears to separate you from the target, {where}. " +
                          "It can be destroyed by attacking."
                        : $"A destructible spider nest is on the direct line to the target, {where}. " +
                          "It can be destroyed by attacking.";

                default:
                    return SeparatesEndpointAreas
                        ? $"A destructible barrier appears to separate you from the target, {where}. " +
                          "It can be destroyed by attacking."
                        : $"A destructible barrier is on the direct line to the target, {where}. " +
                          "It can be destroyed by attacking.";
            }
        }

        public string ForLog()
        {
            var sourceName = Source == null ? "missing" : Source.GetType().Name;
            var objectName = Source == null ? string.Empty : Safe(Source.gameObject.name);
            return $"kind={Kind},source={sourceName},object='{objectName}',id={InstanceId}," +
                   $"world={Format(Position)},targetDoor={IsTargetDoorBarrier}," +
                   $"separatesAreas={SeparatesEndpointAreas},samples={AreaSamples ?? "none"}";
        }

        private string Location(Vector3 playerPosition)
        {
            if (Compass.TryDescribe(playerPosition, Position, out var bearing, out var distance))
                return $"{Compass.DescribeDistance(distance)}, {bearing}";

            return Compass.DescribeDistance(Vector3.Distance(playerPosition, Position));
        }

        private static string Safe(string value) =>
            string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\r", " ").Replace("\n", " ").Replace("'", string.Empty);

        private static string Format(Vector3 value) =>
            $"({value.x:0.00},{value.y:0.00},{value.z:0.00})";
    }

    internal sealed class RouteBlockerInspection
    {
        public readonly List<RouteBlockerEvidence> Candidates = new List<RouteBlockerEvidence>();

        public RouteBlockerEvidence Best
        {
            get
            {
                RouteBlockerEvidence best = null;
                foreach (var candidate in Candidates)
                {
                    if (best == null || Rank(candidate) > Rank(best)) best = candidate;
                }
                return best;
            }
        }

        public string ForLog()
        {
            if (Candidates.Count == 0) return "none";
            var values = new string[Candidates.Count];
            for (var i = 0; i < Candidates.Count; i++) values[i] = Candidates[i].ForLog();
            return string.Join(" | ", values);
        }

        private static int Rank(RouteBlockerEvidence candidate)
        {
            if (candidate.IsTargetDoorBarrier) return 4;
            if (candidate.SeparatesEndpointAreas && candidate.IsDestructible) return 3;
            if (candidate.SeparatesEndpointAreas) return 2;
            return 1;
        }
    }
}
