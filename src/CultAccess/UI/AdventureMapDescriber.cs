using System.Collections.Generic;
using CultAccess.Util;
using Lamb.UI;
using Map;
using UnityEngine.UI;

namespace CultAccess.UI
{
    /// <summary>
    /// Gives the icon-only adventure map nodes the labels a sighted player gets from
    /// their artwork. Availability comes from <see cref="AdventureMapNode.State"/>,
    /// the same state that controls whether the game's button can be confirmed.
    /// </summary>
    internal static class AdventureMapDescriber
    {
        public static bool TryDescribe(Selectable selectable, out string description)
        {
            description = string.Empty;
            if (selectable == null) return false;

            var node = selectable.GetComponentInParent<AdventureMapNode>();
            if (node == null || node.MapNode == null) return false;

            var parts = new List<string>(6) { LabelFor(node) };

            if (SelectableDescriber.Verbosity != Verbosity.Low)
            {
                parts.Add(StateFor(node.State));

                var choice = ChoicePosition(node);
                if (choice != null) parts.Add(choice);

                var blueprint = node.NodeBlueprint;
                if (blueprint != null && blueprint.HasCost)
                {
                    var localizedName = InventoryItem.LocalizedName(blueprint.CostType);
                    var itemName = RichText.IsUsableLocalization(
                        localizedName, $"Inventory/{blueprint.CostType}")
                        ? RichText.Clean(localizedName)
                        : RichText.Humanise(blueprint.CostType.ToString());

                    parts.Add($"requires {blueprint.CostAmount} {itemName}");
                }

                parts.Add("button");
            }

            if (SelectableDescriber.Verbosity == Verbosity.Diagnostic)
            {
                parts.Add($"[{node.NodeType}, point {node.MapNode.point}, {selectable.gameObject.name}]");
            }

            description = string.Join(", ", parts.ToArray());
            Plugin.Log.LogInfo(
                $"[adventure map] type={node.NodeType} hidden={node.MapNode.Hidden} " +
                $"state={node.State} point={node.MapNode.point} spoken=\"{description}\"");
            return true;
        }

        private static string LabelFor(AdventureMapNode node)
        {
            // A hidden node is rendered as a question mark. Do not reveal its backing
            // blueprint: that would turn accessibility support into advance knowledge.
            if (node.MapNode.Hidden) return "Unknown route";

            var blueprint = node.NodeBlueprint;
            var type = blueprint != null ? blueprint.nodeType : node.NodeType;

            switch (type)
            {
                case NodeType.MinorEnemy:
                case NodeType.FirstFloor:
                case NodeType.DungeonFloor:
                    return "Combat route";
                case NodeType.EliteEnemy:
                    return "Elite combat route";
                case NodeType.RestSite:
                case NodeType.Special_Healing:
                    return "Healing route";
                case NodeType.Treasure:
                case NodeType.Special_RewardChoice:
                    return "Reward route";
                case NodeType.Store:
                    return "Shop route";
                case NodeType.Boss:
                    return "Bishop fight";
                case NodeType.FinalBoss:
                    return "Final boss fight";
                case NodeType.MiniBossFloor:
                case NodeType.Negative_PreviousMiniboss:
                    return "Miniboss route";
                case NodeType.Mystery:
                    return "Mystery route";
                case NodeType.Follower:
                case NodeType.Follower_Beginner:
                case NodeType.Follower_Easy:
                case NodeType.Follower_Medium:
                case NodeType.Follower_Hard:
                    return "Follower rescue route";
                case NodeType.Gold:
                    return "Gold route";
                case NodeType.Rare_Gold:
                    return "Rare gold route";
                case NodeType.Wood:
                    return "Lumber route";
                case NodeType.Stone:
                    return "Stone route";
                case NodeType.Food:
                    return "Food route";
                case NodeType.Meat:
                    return "Meat route";
                case NodeType.Bones:
                    return "Bones route";
                case NodeType.Poop:
                    return "Fertilizer route";
                case NodeType.Tarot:
                    return "Tarot card route";
                case NodeType.Fishing:
                    return "Fishing route";
                case NodeType.Special_Teleporter:
                case NodeType.Intro_TeleportHome:
                    return "Exit route";
                case NodeType.Special_BloodSacrafice:
                    return "Blood sacrifice route";
                case NodeType.special_Challenge:
                    return "Challenge route";
                case NodeType.Special_CoinGamble:
                    return "Coin gamble route";
                case NodeType.Special_HealthChoice:
                    return "Health choice route";
                case NodeType.Special_FindRelic:
                    return "Relic route";
                case NodeType.FindNote:
                    return "Lore route";
                default:
                    return RichText.Humanise(type.ToString()) + " route";
            }
        }

        private static string StateFor(NodeStates state)
        {
            switch (state)
            {
                case NodeStates.Attainable: return "available";
                case NodeStates.Visited: return "completed";
                default: return "locked";
            }
        }

        private static string ChoicePosition(AdventureMapNode selected)
        {
            var overlay = selected.GetComponentInParent<UIAdventureMapOverlayController>();
            if (overlay == null) return null;

            var choices = new List<AdventureMapNode>();
            foreach (var candidate in overlay.GetComponentsInChildren<AdventureMapNode>(true))
            {
                if (candidate == null || candidate.MapNode == null) continue;
                if (candidate.State != NodeStates.Attainable) continue;
                if (candidate.MapNode.point.y != selected.MapNode.point.y) continue;
                choices.Add(candidate);
            }

            choices.Sort((left, right) => left.MapNode.point.x.CompareTo(right.MapNode.point.x));
            var index = choices.IndexOf(selected);
            return index < 0 ? null : $"choice {index + 1} of {choices.Count}";
        }
    }
}
