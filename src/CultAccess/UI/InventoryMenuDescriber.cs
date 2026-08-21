using System;
using System.Collections.Generic;
using System.Reflection;
using CultAccess.Diagnostics;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;
using Lamb.UI.Menus.PlayerMenu;
using Lamb.UI.PauseDetails;
using Rewired;
using src.UI.Items;
using UnityEngine.UI;

namespace CultAccess.UI
{
    /// <summary>Supplies names and live values for icon-only inventory/player-menu entries.</summary>
    internal static class InventoryMenuDescriber
    {
        private static readonly FieldInfo CurrencyItemsField = AccessTools.Field(
            typeof(InventoryMenu), "_currencies");
        private static readonly FieldInfo FoodItemsField = AccessTools.Field(
            typeof(InventoryMenu), "_food");
        private static readonly FieldInfo OtherItemsField = AccessTools.Field(
            typeof(InventoryMenu), "_items");

        internal static bool TryDescribe(Selectable selectable, out string description)
        {
            description = string.Empty;
            if (selectable == null) return false;

            // Not gated on InventoryMenu. The game reuses GenericInventoryItem in
            // UIItemSelectorOverlayController — the overlay a trade, an offering or a feeding
            // puts up — and requiring the menu sent those entries to the generic reader.
            // The menu is still resolved, because it is what supplies the category and
            // position, and because it is what makes the entry read-only.
            var inventory = selectable.GetComponentInParent<GenericInventoryItem>();
            if (inventory != null)
            {
                description = DescribeInventoryItem(
                    selectable.GetComponentInParent<InventoryMenu>(), inventory);
                return true;
            }

            var weapon = selectable.GetComponentInParent<WeaponItem>();
            if (weapon != null)
            {
                description = DescribeEquipment(weapon.Type, "weapon", CurrentPlayer()?.currentWeaponLevel ?? 0);
                return true;
            }

            var curse = selectable.GetComponentInParent<CurseItem>();
            if (curse != null)
            {
                description = DescribeEquipment(curse.Type, "curse", CurrentPlayer()?.currentCurseLevel ?? 0);
                return true;
            }

            var relic = selectable.GetComponentInParent<RelicPlayerMenuItem>();
            if (relic != null)
            {
                description = DescribeRelic(relic.Data, "equipped relic");
                return true;
            }

            var activeRelic = selectable.GetComponentInParent<ActiveRelicItem>();
            if (activeRelic != null)
            {
                description = DescribeRelic(activeRelic.RelicData, "active relic effect");
                return true;
            }

            var fleece = selectable.GetComponentInParent<FleeceItem>();
            if (fleece != null)
            {
                description = DescribeFleece();
                return true;
            }

            var crown = selectable.GetComponentInParent<CrownAbilityItem>();
            if (crown != null)
            {
                description = DescribeUpgrade(crown.Type, "crown ability");
                return true;
            }

            if (selectable.GetComponentInParent<TalismanPiecesItem>() != null)
            {
                var pieces = DataManager.Instance?.CurrentKeyPieces ?? 0;
                description = $"Holy Talisman fragments, {pieces} of 4";
                return true;
            }

            if (selectable.GetComponentInParent<DoctrineFragmentsItem>() != null)
            {
                var pieces = DataManager.Instance?.DoctrineCurrentCount ?? 0;
                var available = DoctrineUpgradeSystem.TrySermonsStillAvailable()
                    ? $"{pieces} of 3 fragments"
                    : "all standard doctrines declared";
                description = $"Commandment Stone, {available}";
                return true;
            }

            return false;
        }

        internal static bool TryDescribeContext(Selectable selectable, out string context)
        {
            context = string.Empty;
            if (selectable == null) return false;

            if (selectable.GetComponentInParent<InventoryMenu>() != null)
            {
                context = $"Inventory. Direction keys review items. {PageKeys()} switch player pages. " +
                          $"{Key(39, "back")} closes";
                return true;
            }

            if (selectable.GetComponentInParent<CharacterMenu>() != null)
            {
                context = $"Player details. Direction keys review equipment and effects. " +
                          $"{PageKeys()} switch player pages. {Key(39, "back")} closes";
                return true;
            }

            return false;
        }

        /// <summary>
        /// <paramref name="menu"/> may be null: the same entry component is used by the item
        /// selector overlay, which has no <see cref="InventoryMenu"/> above it.
        ///
        /// "Read only" is said only for the real inventory menu, and that is the reason this
        /// takes a nullable menu rather than simply dropping the requirement. In the selector
        /// overlay the entry is the thing being chosen, so announcing it as read-only would
        /// tell the player the opposite of the truth.
        /// </summary>
        private static string DescribeInventoryItem(
            InventoryMenu menu, GenericInventoryItem item)
        {
            var name = ItemName(item.Type);
            var category = "inventory";
            var index = -1;
            var count = 0;

            if (menu != null)
            {
                if (TryPosition(CurrencyItemsField, menu, item, out index, out count))
                    category = "currency and resources";
                else if (TryPosition(FoodItemsField, menu, item, out index, out count))
                    category = "food";
                else if (TryPosition(OtherItemsField, menu, item, out index, out count))
                    category = "items";
            }
            else if (TrySelectorPosition(item, out index, out count))
            {
                category = "choice";
            }

            var position = index >= 0
                ? $". {ProgressionText.Position($"{category} item", index, count)}"
                : string.Empty;
            var access = menu != null ? ". Read only" : string.Empty;
            return $"{name}, {item.Quantity} owned{position}{access}";
        }

        /// <summary>
        /// Position within the item selector overlay, counted from its live children. The
        /// overlay keeps its entries in a private list, and the children are the same set in
        /// the same order without the reflection.
        /// </summary>
        private static bool TrySelectorPosition(
            GenericInventoryItem item, out int index, out int count)
        {
            index = -1;
            count = 0;

            var overlay = item.GetComponentInParent<UIItemSelectorOverlayController>();
            if (overlay == null) return false;

            var items = overlay.GetComponentsInChildren<GenericInventoryItem>(false);
            if (items == null) return false;

            index = Array.IndexOf(items, item);
            count = items.Length;
            return index >= 0;
        }

        private static string DescribeEquipment(EquipmentType type, string kind, int level)
        {
            if (type == EquipmentType.None) return $"No {kind} equipped";
            try
            {
                var data = EquipmentManager.GetEquipmentData(type);
                var name = CleanOrFallback(data?.GetLocalisedTitle(), type.ToString());
                var description = RichText.Clean(data?.GetLocalisedDescription());
                var levelText = level > 0 ? $", level {level}" : string.Empty;
                return description.Length == 0
                    ? $"{name}, equipped {kind}{levelText}"
                    : $"{name}, equipped {kind}{levelText}. {description}";
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not describe equipped {kind}: {e.Message}");
                return $"{RichText.Humanise(type.ToString())}, equipped {kind}";
            }
        }

        private static string DescribeRelic(RelicData data, string role)
        {
            if (data == null) return "No relic equipped";
            try
            {
                var name = CleanOrFallback(
                    RelicData.GetTitleLocalisation(data.RelicType), data.RelicType.ToString());
                var effect = RichText.Clean(
                    RelicData.GetDescriptionLocalisation(data.RelicType));
                return effect.Length == 0 ? $"{name}, {role}" : $"{name}, {role}. {effect}";
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not describe player relic: {e.Message}");
                return $"{RichText.Humanise(data.RelicType.ToString())}, {role}";
            }
        }

        private static string DescribeFleece()
        {
            var fleece = DataManager.Instance?.PlayerFleece ?? 0;
            try
            {
                var name = CleanOrFallback(
                    FleeceSelectionMenu.LocalisedName(fleece.ToString()), $"Fleece {fleece}");
                var effect = RichText.Clean(
                    FleeceSelectionMenu.LocalisedDescription(fleece.ToString()));
                return effect.Length == 0
                    ? $"{name}, equipped fleece"
                    : $"{name}, equipped fleece. {effect}";
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not describe equipped fleece: {e.Message}");
                return $"Fleece {fleece}, equipped";
            }
        }

        private static string DescribeUpgrade(UpgradeSystem.Type type, string role)
        {
            try
            {
                var name = CleanOrFallback(UpgradeSystem.GetLocalizedName(type), type.ToString());
                var effect = RichText.Clean(UpgradeSystem.GetLocalizedDescription(type));
                var state = UpgradeSystem.GetUnlocked(type) ? "unlocked" : "locked";
                return effect.Length == 0
                    ? $"{name}, {role}, {state}"
                    : $"{name}, {role}, {state}. {effect}";
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not describe player upgrade: {e.Message}");
                return $"{RichText.Humanise(type.ToString())}, {role}";
            }
        }

        private static bool TryPosition(
            FieldInfo field, InventoryMenu menu, GenericInventoryItem target,
            out int index, out int count)
        {
            index = -1;
            count = 0;
            try
            {
                var items = field?.GetValue(menu) as List<GenericInventoryItem>;
                if (items == null || !items.Contains(target)) return false;
                index = items.IndexOf(target);
                count = items.Count;
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read inventory item position: {e.Message}");
                return false;
            }
        }

        private static PlayerFarming CurrentPlayer() =>
            CharacterMenu.OpeningPlayerFarming != null
                ? CharacterMenu.OpeningPlayerFarming
                : PlayerFarming.Instance;

        private static string ItemName(InventoryItem.ITEM_TYPE type)
        {
            try
            {
                var localized = RichText.Clean(InventoryItem.LocalizedName(type));
                return RichText.IsUsableLocalization(localized, $"Inventory/{type}")
                    ? localized
                    : RichText.Humanise(type.ToString());
            }
            catch
            {
                return RichText.Humanise(type.ToString());
            }
        }

        private static string PageKeys()
        {
            var previous = Key(43, "previous page");
            var next = Key(44, "next page");
            return $"{previous} and {next}";
        }

        private static string Key(int action, string fallback)
        {
            var key = BindingDump.KeyboardBindingsForAction(action);
            return key.Length > 0 ? key : fallback;
        }

        private static string CleanOrFallback(string value, string fallback)
        {
            var clean = RichText.Clean(value);
            return clean.Length > 0 ? clean : RichText.Humanise(fallback);
        }
    }
}
