using System;
using Pathfinding;
using UnityEngine;

namespace CultAccess.Navigation
{
    internal enum RouteReachabilityState
    {
        Unknown,
        Reachable,
        Disconnected,
    }

    /// <summary>
    /// A live, immutable description of the two graph nodes used by the same connected-
    /// component test that gates route requests. Storing values rather than GraphNode
    /// references keeps diagnostics safe when a room replaces the entire A* instance.
    /// </summary>
    internal sealed class ReachabilitySnapshot
    {
        public RouteReachabilityState State;
        public string UnknownReason;
        public int GraphInstanceId;
        public Vector3 PlayerPosition;
        public Vector3 TargetPosition;
        public Vector3 PlayerNodePosition;
        public Vector3 TargetNodePosition;
        public uint PlayerArea;
        public uint TargetArea;
        public uint PlayerGraphIndex;
        public uint TargetGraphIndex;
        public uint PlayerTag;
        public uint TargetTag;
        public bool PlayerWalkable;
        public bool TargetWalkable;
        public float NodeSize;

        public static ReachabilitySnapshot Inspect(Vector3 from, Vector3 to)
        {
            var result = new ReachabilitySnapshot
            {
                State = RouteReachabilityState.Unknown,
                PlayerPosition = from,
                TargetPosition = to,
            };

            var graph = AstarPath.active;
            if (graph == null)
            {
                result.UnknownReason = "no active AstarPath";
                return result;
            }

            result.GraphInstanceId = graph.GetInstanceID();

            try
            {
                // Mirror UnitObject.IsPathPossible rather than the unconstrained nearest-
                // node probe. The latest log showed that probe returning an unwalkable area
                // 0 beneath the player's collider at a room edge. Applying the same default
                // constraint as the game removes that mismatch from the preflight gate.
                var fromInfo = graph.GetNearest(from, NNConstraint.Default);
                var toInfo = graph.GetNearest(to, NNConstraint.Default);
                var fromNode = fromInfo.node;
                var toNode = toInfo.node;
                if (fromNode == null || toNode == null)
                {
                    result.UnknownReason = fromNode == null && toNode == null
                        ? "both nearest graph nodes are missing"
                        : fromNode == null ? "player graph node is missing" : "target graph node is missing";
                    return result;
                }

                result.PlayerNodePosition = fromInfo.position;
                result.TargetNodePosition = toInfo.position;
                result.PlayerArea = fromNode.Area;
                result.TargetArea = toNode.Area;
                result.PlayerGraphIndex = fromNode.GraphIndex;
                result.TargetGraphIndex = toNode.GraphIndex;
                result.PlayerTag = fromNode.Tag;
                result.TargetTag = toNode.Tag;
                result.PlayerWalkable = fromNode.Walkable;
                result.TargetWalkable = toNode.Walkable;
                result.NodeSize = ResolveNodeSize(fromNode, toNode);
                result.State = PathUtilities.IsPathPossible(fromNode, toNode)
                    ? RouteReachabilityState.Reachable
                    : RouteReachabilityState.Disconnected;
            }
            catch (Exception e)
            {
                result.UnknownReason = e.Message;
                Plugin.Log.LogWarning($"Reachability check failed: {e.Message}");
            }

            return result;
        }

        public bool TopologyDiffersFrom(ReachabilitySnapshot other)
        {
            if (other == null) return true;
            return State != other.State ||
                   GraphInstanceId != other.GraphInstanceId ||
                   PlayerArea != other.PlayerArea || TargetArea != other.TargetArea ||
                   PlayerGraphIndex != other.PlayerGraphIndex ||
                   TargetGraphIndex != other.TargetGraphIndex ||
                   PlayerWalkable != other.PlayerWalkable ||
                   TargetWalkable != other.TargetWalkable;
        }

        private static float ResolveNodeSize(GraphNode fromNode, GraphNode toNode)
        {
            var grid = fromNode.Graph as GridGraph ?? toNode.Graph as GridGraph;
            return grid == null ? 0f : grid.nodeSize;
        }
    }
}
