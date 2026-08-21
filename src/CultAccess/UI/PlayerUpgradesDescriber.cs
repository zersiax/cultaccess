using System.Collections.Generic;
using CultAccess.Util;
using I2.Loc;
using Lamb.UI;
using UnityEngine.UI;

namespace CultAccess.UI
{
    /// <summary>
    /// Reads the Player Upgrades window reached through the Temple Altar's Crown option.
    ///
    /// Its tiles are the icon-only pattern in its purest form: neither
    /// <c>CrownAbilityItemBuyable</c> nor <c>FleeceItemBuyable</c> holds a name field at all.
    /// The only text on either is the price, so the generic reader announced the cost and
    /// never the item — live logs show "monster Heart 1, button", which is one Monster Heart
    /// and not the name of anything. Both tiles do carry the identifier the game itself uses
    /// to look the item up, which is all a description needs.
    ///
    /// The third tile type in this window is a <see cref="RitualItem"/>, reused outside the
    /// rituals menu; <see cref="RitualDoctrineDescriber"/> covers it.
    /// </summary>
    internal static class PlayerUpgradesDescriber
    {
        internal static bool TryDescribe(Selectable selectable, out string description)
        {
            description = string.Empty;
            if (selectable == null) return false;

            var ability = selectable.GetComponentInParent<CrownAbilityItemBuyable>();
            if (ability != null)
            {
                description = DescribeAbility(ability);
                return true;
            }

            var fleece = selectable.GetComponentInParent<FleeceItemBuyable>();
            if (fleece != null)
            {
                description = DescribeFleece(fleece);
                return true;
            }

            return false;
        }

        private static string DescribeAbility(CrownAbilityItemBuyable item)
        {
            var type = item.Type;
            var unlocked = UpgradeSystem.GetUnlocked(type);
            var parts = new List<string>(5)
            {
                CleanOrFallback(
                    UpgradeSystem.GetLocalizedName(type),
                    type.ToString(),
                    $"UpgradeSystem/{type}/Name"),
            };

            if (SelectableDescriber.Verbosity != Verbosity.Low)
            {
                parts.Add(State(unlocked, item.Cost.CanAfford()));

                // The cost text is blanked once an upgrade is owned, so repeating the price
                // of something already bought would be noise rather than information.
                if (!unlocked)
                {
                    var cost = Cost(item.Cost);
                    if (cost.Length > 0) parts.Add(cost);
                }

                var prose = UpgradeSystem.GetLocalizedDescription(type);
                if (RichText.IsUsableLocalization(prose, $"UpgradeSystem/{type}/Description"))
                    parts.Add(RichText.Clean(prose));

                AddPosition(parts, item);
            }

            if (SelectableDescriber.Verbosity == Verbosity.Diagnostic)
                parts.Add($"[{type}]");

            var description = string.Join(", ", parts.ToArray());
            Plugin.Log.LogInfo(
                $"[player upgrades] kind=crown-ability type={type} unlocked={unlocked} " +
                $"affordable={item.Cost.CanAfford()} spoken=\"{description}\"");
            return description;
        }

        private static string DescribeFleece(FleeceItemBuyable item)
        {
            var fleece = (PlayerFleeceManager.FleeceType)item.Fleece;

            // Keyed by the numeric index, matching FleeceSelectionMenu, which is the game's
            // own working lookup. PlayerFleeceManager.GetFleeceDisplayName builds
            // "TarotCards/{enum name}/Name" instead and resolves for nothing but the default
            // fleece, whose enum name happens to be handled separately — every other fleece
            // read as a bare "Gold", "Green" or "Purple" with no description at all.
            var term = $"TarotCards/Fleece{item.Fleece}";

            var parts = new List<string>(5)
            {
                CleanOrFallback(
                    LocalizationManager.GetTranslation($"{term}/Name"),
                    fleece.ToString(),
                    $"{term}/Name"),
                "fleece",
            };

            if (SelectableDescriber.Verbosity != Verbosity.Low)
            {
                parts.Add(State(item.Unlocked, item.Cost.CanAfford()));

                if (!item.Unlocked)
                {
                    var cost = Cost(item.Cost);
                    if (cost.Length > 0) parts.Add(cost);
                }

                var prose = LocalizationManager.GetTranslation($"{term}/Description");
                if (RichText.IsUsableLocalization(prose, $"{term}/Description"))
                    parts.Add(RichText.Clean(prose));

                AddPosition(parts, item);
            }

            var description = string.Join(", ", parts.ToArray());
            Plugin.Log.LogInfo(
                $"[player upgrades] kind=fleece type={fleece} unlocked={item.Unlocked} " +
                $"affordable={item.Cost.CanAfford()} spoken=\"{description}\"");
            return description;
        }

        /// <summary>
        /// Mirrors the tile's own rule: <c>Confirmable = cost.CanAfford() &amp;&amp; !unlocked</c>.
        /// "Cannot afford" is said rather than a bare "unavailable" because the reason is the
        /// actionable half — the cost that follows tells the player how far off they are.
        /// </summary>
        private static string State(bool unlocked, bool affordable)
        {
            if (unlocked) return "already unlocked";
            return affordable ? "available" : "cannot afford";
        }

        private static string Cost(StructuresData.ItemCost cost)
        {
            if (cost == null || cost.CostValue <= 0) return string.Empty;

            var name = CleanOrFallback(
                InventoryItem.LocalizedName(cost.CostItem),
                cost.CostItem.ToString(),
                $"Inventory/{cost.CostItem}");
            var owned = Inventory.GetItemQuantity((int)cost.CostItem);
            return $"cost {cost.CostValue} {name}, have {owned}";
        }

        /// <summary>
        /// Position within the window's own row of tiles. Counted from the live children
        /// rather than the controller's private arrays, which are split into base and DLC
        /// sets and would number a DLC tile as if it were the first.
        /// </summary>
        private static void AddPosition<T>(List<string> parts, T item)
            where T : UnityEngine.Component
        {
            var menu = item.GetComponentInParent<UIPlayerUpgradesMenuController>();
            var siblings = menu == null ? null : menu.GetComponentsInChildren<T>(false);
            if (siblings == null) return;

            var index = System.Array.IndexOf(siblings, item);
            if (index >= 0)
                parts.Add(ProgressionText.Position("item", index, siblings.Length));
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
