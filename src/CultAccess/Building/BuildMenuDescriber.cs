using System.Collections.Generic;
using CultAccess.UI;
using CultAccess.Util;
using Lamb.UI.BuildMenu;
using UnityEngine.UI;

namespace CultAccess.Building
{
    /// <summary>Reads the build catalogue's icon-only entries and their info-card data.</summary>
    internal static class BuildMenuDescriber
    {
        public static bool TryDescribe(Selectable selectable, out string description)
        {
            description = string.Empty;
            if (selectable == null) return false;

            var item = selectable.GetComponentInParent<BuildMenuItem>();
            if (item == null) return false;

            var type = item.Structure;
            var parts = new List<string>(7)
            {
                CleanOrFallback(
                    StructuresData.LocalizedName(type), type.ToString(), $"Structures/{type}")
            };

            if (SelectableDescriber.Verbosity != Verbosity.Low)
            {
                parts.Add(Availability(item));

                var cost = Cost(type);
                if (cost.Length > 0) parts.Add(cost);

                var rawProse = StructuresData.LocalizedDescription(type);
                if (RichText.IsUsableLocalization(rawProse, $"Structures/{type}/Description"))
                    parts.Add(RichText.Clean(rawProse));

                var position = Position(item);
                if (position != null) parts.Add(position);

                parts.Add("button");
            }

            if (SelectableDescriber.Verbosity == Verbosity.Diagnostic)
                parts.Add($"[{type}, {selectable.gameObject.name}]");

            description = string.Join(", ", parts.ToArray());
            Plugin.Log.LogInfo(
                $"[build menu] type={type} locked={item.Locked} " +
                $"confirmable={item.Button?.Confirmable} spoken=\"{description}\"");
            return true;
        }

        private static string Availability(BuildMenuItem item)
        {
            var type = item.Structure;
            if (item.Button != null && item.Button.Confirmable) return "available";
            if (item.Locked) return "locked";

            if (StructuresData.RequiresTempleToBuild(type) && !StructuresData.HasTemple())
                return "unavailable, requires Temple";

            if (StructuresData.RequiresRanchToBuild(type) &&
                StructureManager.GetAllStructuresOfType<Structures_Ranch>().Count == 0)
                return "unavailable, requires Ranch";

            if (StructuresData.GetBuildOnlyOne(type) &&
                (StructureManager.IsBuilt(type) || StructureManager.IsBuilding(type) ||
                 StructureManager.IsAnyUpgradeBuiltOrBuilding(type)))
                return "unavailable, already built or under construction";

            if (StructuresData.IsUpgradeStructure(type))
            {
                var prerequisite = StructuresData.GetUpgradePrerequisite(type);
                if (StructureManager.GetAllStructuresOfType(prerequisite).Count == 0)
                {
                    var name = CleanOrFallback(
                        StructuresData.LocalizedName(prerequisite), prerequisite.ToString(),
                        $"Structures/{prerequisite}");
                    return "unavailable, requires " + name;
                }
            }

            if (!CheatConsole.BuildingsFree && !StructuresData.CanAfford(type))
                return "unavailable, cannot afford";

            return "unavailable";
        }

        private static string Cost(StructureBrain.TYPES type)
        {
            var costs = StructuresData.GetCost(type);
            if (costs == null || costs.Count == 0) return string.Empty;

            var parts = new List<string>();
            foreach (var cost in costs)
            {
                var name = CleanOrFallback(
                    InventoryItem.LocalizedName(cost.CostItem), cost.CostItem.ToString(),
                    $"Inventory/{cost.CostItem}");
                var owned = Inventory.GetItemQuantity((int)cost.CostItem);
                parts.Add($"{cost.CostValue} {name}, have {owned}");
            }

            return "cost " + string.Join(", ", parts.ToArray());
        }

        private static string Position(BuildMenuItem item)
        {
            var category = item.GetComponentInParent<BuildMenuCategory>();
            if (category == null || category.BuildItems == null) return null;

            var index = category.BuildItems.IndexOf(item);
            return index < 0 ? null : $"item {index + 1} of {category.BuildItems.Count}";
        }

        private static string CleanOrFallback(
            string localized, string fallback, string expectedTerm)
        {
            if (RichText.IsUsableLocalization(localized, expectedTerm))
                return RichText.Clean(localized);
            return RichText.Humanise(fallback);
        }
    }
}
