using System;
using System.Collections.Generic;
using System.Reflection;
using CultAccess.Diagnostics;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;
using Lamb.UI.Menus.DoctrineChoicesMenu;
using Lamb.UI.Rituals;
using MMTools;
using Rewired;
using UnityEngine;
using UnityEngine.UI;
using src.UINavigator;

namespace CultAccess.UI
{
    /// <summary>Semantic descriptions for icon-driven ritual and doctrine screens.</summary>
    internal static class RitualDoctrineDescriber
    {
        private static readonly FieldInfo AttachedMenuField = AccessTools.Field(
            typeof(UIMenuControlPrompts), "_attachedMenu");
        private static readonly FieldInfo ShownRitualField = AccessTools.Field(
            typeof(UIRitualsMenuController), "_showRitual");
        private static readonly FieldInfo ForceRitualField = AccessTools.Field(
            typeof(UIRitualsMenuController), "_forceDoRitual");

        private static UIDoctrineChoiceInfoBox _confirmingDoctrine;

        internal static bool TryDescribe(Selectable selectable, out string description)
        {
            description = string.Empty;
            if (selectable == null) return false;

            // Deliberately not gated on UIRitualsMenuController. The game reuses RitualItem
            // as a tile in the Player Upgrades menu reached through the altar's Crown option,
            // where there is no rituals controller in the parent chain. That gate sent those
            // tiles to the generic reader, which found only the cost label and announced
            // "Doctrine Stone 1, button" and "On Cooldown, button" — the price, never the
            // ritual. DescribeRitual needs nothing but the RitualItem, and AddRitualPosition
            // already omits the position when there is no owning menu to count against.
            var ritual = selectable.GetComponentInParent<RitualItem>();
            if (ritual != null)
            {
                description = DescribeRitual(ritual, includeControl: true);
                return true;
            }

            var offeredDoctrine = selectable.GetComponentInParent<UIDoctrineChoiceInfoBox>();
            if (offeredDoctrine != null)
            {
                description = DescribeDoctrineOffer(offeredDoctrine, includeControl: true);
                return true;
            }

            var doctrine = selectable.GetComponentInParent<DoctrineChoice>();
            if (doctrine != null && doctrine.GetComponentInParent<DoctrineSubmenu>() != null)
            {
                description = DescribeDoctrineHistory(doctrine);
                return true;
            }

            return false;
        }

        internal static bool TryDescribeContext(Selectable selectable, out string context)
        {
            context = string.Empty;
            if (selectable == null) return false;

            if (selectable.GetComponentInParent<UIRitualsMenuController>() != null)
            {
                context = $"Rituals. Direction keys review rituals. {Key(38, "confirm")} " +
                          $"selects an available ritual. {Key(39, "back")} goes back";
                return true;
            }

            if (selectable.GetComponentInParent<UIDoctrineChoicesMenuController>() != null)
            {
                context = "Choose a doctrine. Left and right review the two choices";
                return true;
            }

            if (selectable.GetComponentInParent<DoctrineSubmenu>() != null)
            {
                context = $"Doctrine history. Direction keys review doctrine slots. " +
                          $"{Key(43, "previous page")} and {Key(44, "next page")} " +
                          $"switch doctrine categories. {Key(39, "back")} goes back";
                return true;
            }

            return false;
        }

        internal static bool TryDescribePanel(UIMenuBase menu, out string description)
        {
            description = string.Empty;
            if (!(menu is UIDoctrineChoicesMenuController doctrineMenu)) return false;

            var focused = MonoSingleton<UINavigatorNew>.Instance?
                .CurrentSelectable?.Selectable;
            if (focused != null && focused.transform.IsChildOf(doctrineMenu.transform) &&
                TryDescribe(focused, out description))
                return true;

            if (_confirmingDoctrine != null &&
                _confirmingDoctrine.transform.IsChildOf(doctrineMenu.transform))
            {
                description = $"Confirm {DescribeDoctrineOffer(_confirmingDoctrine, false)}. " +
                              DoctrineConfirmationInstruction();
                return true;
            }

            var offers = doctrineMenu.GetComponentsInChildren<UIDoctrineChoiceInfoBox>(false);
            if (offers.Length == 0)
            {
                description = "Doctrine choices are not ready yet";
                return true;
            }

            var parts = new List<string>();
            for (var index = 0; index < offers.Length; index++)
                parts.Add($"Choice {index + 1}. {DescribeDoctrineOffer(offers[index], false)}");
            description = string.Join(". ", parts.ToArray());
            return true;
        }

        internal static string DescribeRitual(RitualItem item, bool includeControl)
        {
            if (item == null) return "Ritual details unavailable";

            var parts = new List<string>();
            if (item.Locked)
            {
                parts.Add("Locked ritual");
                AddRitualPosition(parts, item);
                parts.Add("details hidden until unlocked");
                return string.Join(". ", parts.ToArray());
            }

            var type = item.RitualType;
            parts.Add(UpgradeName(type));
            parts.Add(RitualState(item));

            var description = Clean(UpgradeSystem.GetLocalizedDescription(type));
            if (description.Length > 0) parts.Add(description);

            AddRitualEffects(parts, type);
            AddRitualCosts(parts, item);
            AddRitualPosition(parts, item);

            if (includeControl && CanChoose(item))
                parts.Add($"{Key(38, "confirm")} selects");

            return string.Join(". ", parts.ToArray());
        }

        internal static string DescribeDoctrineOffer(
            UIDoctrineChoiceInfoBox box, bool includeControl)
        {
            if (box == null || box.Info == null) return "Doctrine choice unavailable";

            var info = box.Info;
            var doctrine = DoctrineUpgradeSystem.GetSermonReward(
                info.SermonCategory, info.RewardLevel, info.isFirstChoice);
            var parts = new List<string>
            {
                DoctrineName(doctrine),
                Clean(DoctrineUpgradeSystem.GetLocalizedDescription(doctrine)),
            };

            var unlock = Clean(DoctrineUpgradeSystem.GetDoctrineUnlockString(doctrine));
            if (unlock.Length > 0) parts.Add(unlock);

            var category = Clean(
                DoctrineUpgradeSystem.GetSermonCategoryLocalizedName(info.SermonCategory));
            if (category.Length > 0) parts.Add($"{category}, level {info.RewardLevel}");

            var siblings = box.GetComponentInParent<UIDoctrineChoicesMenuController>()?
                .GetComponentsInChildren<UIDoctrineChoiceInfoBox>(false);
            var index = siblings == null ? -1 : Array.IndexOf(siblings, box);
            if (index >= 0)
                parts.Add(ProgressionText.Position("choice", index, siblings.Length));

            if (includeControl)
            {
                var accept = Key(38, "confirm");
                parts.Add(ProgressionText.ChooseThenConfirm(
                    accept, SettingsManager.Settings.Accessibility.HoldActions, "declare"));
            }

            parts.RemoveAll(string.IsNullOrEmpty);
            return string.Join(". ", parts.ToArray());
        }

        internal static void AnnounceDoctrineConfirmation(UIDoctrineChoiceInfoBox box)
        {
            if (box == null || !Plugin.SpeechEnabled.Value) return;
            _confirmingDoctrine = box;
            var description = DescribeDoctrineOffer(box, includeControl: false);
            Speaker.SayParts(
                SpeechPriority.Now,
                $"Confirm {description}",
                DoctrineConfirmationInstruction());
        }

        internal static void DoctrineUnlocked() => _confirmingDoctrine = null;

        internal static void AnnounceRitualSelected(RitualItem item)
        {
            if (item == null || !Plugin.SpeechEnabled.Value) return;
            Speaker.Say($"Selected {UpgradeName(item.RitualType)}", SpeechPriority.Now);
        }

        internal static void OnAcceptPromptShown(UIMenuControlPrompts prompts)
        {
            if (prompts == null || !Plugin.SpeechEnabled.Value) return;

            UIRitualsMenuController menu;
            try { menu = AttachedMenuField?.GetValue(prompts) as UIRitualsMenuController; }
            catch { return; }
            if (menu == null) return;

            UpgradeSystem.Type type;
            bool forced;
            try
            {
                type = ShownRitualField == null
                    ? UpgradeSystem.Type.Count
                    : (UpgradeSystem.Type)ShownRitualField.GetValue(menu);
                forced = ForceRitualField != null && (bool)ForceRitualField.GetValue(menu);
            }
            catch { return; }

            if (type == UpgradeSystem.Type.Count) return;
            var control = forced
                ? $"{Key(38, "confirm")} performs this ritual"
                : $"{Key(38, "confirm")} continues";
            Speaker.Say($"{UpgradeName(type)} unlocked. {control}", SpeechPriority.Now);
        }

        private static string DescribeDoctrineHistory(DoctrineChoice choice)
        {
            var parts = new List<string>();
            switch (choice.CurrentState)
            {
                case DoctrineChoice.State.Locked:
                    parts.Add("Locked doctrine slot");
                    parts.Add("unlocks by declaring more doctrines in this category");
                    break;
                case DoctrineChoice.State.Unchosen:
                    parts.Add("Unchosen doctrine slot");
                    parts.Add("the opposing doctrine was declared");
                    break;
                default:
                {
                    var type = choice.Type;
                    parts.Add(DoctrineName(type));
                    parts.Add("declared doctrine");
                    if (choice.UnlockedWithCrystal) parts.Add("declared with a Forgotten Commandment Stone");
                    var effect = Clean(DoctrineUpgradeSystem.GetLocalizedDescription(type));
                    if (effect.Length > 0) parts.Add(effect);
                    var unlock = Clean(DoctrineUpgradeSystem.GetDoctrineUnlockString(type));
                    if (unlock.Length > 0) parts.Add(unlock);
                    var category = DoctrineUpgradeSystem.GetCategory(type);
                    var categoryName = Clean(
                        DoctrineUpgradeSystem.GetSermonCategoryLocalizedName(category));
                    var level = DoctrineLevel(type, category);
                    if (categoryName.Length > 0)
                        parts.Add(level > 0 ? $"{categoryName}, level {level}" : categoryName);
                    break;
                }
            }

            var submenu = choice.GetComponentInParent<DoctrineSubmenu>();
            var choices = submenu?.GetComponentsInChildren<DoctrineChoice>(false);
            var index = choices == null ? -1 : Array.IndexOf(choices, choice);
            if (index >= 0)
                parts.Add(ProgressionText.Position("slot", index, choices.Length));
            parts.Add("read only");
            return string.Join(". ", parts.ToArray());
        }

        private static string RitualState(RitualItem item)
        {
            if (item.Maxed) return "already completed, unavailable";
            if (item.Cooldown)
            {
                var remaining = Mathf.RoundToInt(Mathf.Clamp01(
                    UpgradeSystem.GetCoolDownNormalised(item.RitualType)) * 100f);
                return $"on cooldown, {remaining} percent remaining";
            }
            // RemoveCost deliberately overrides the ordinary special-eligibility gate
            // for a forced story ritual. Confirmable is the final state the navigator uses.
            if (!item.Available && (item.Button == null || !item.Button.Confirmable))
                return SpecialRequirement(item.RitualType);
            if (item.Button != null && !item.Button.Confirmable && !item.Free &&
                !UpgradeSystem.UserCanAffordUpgrade(item.RitualType))
                return "unavailable, cannot afford";
            return item.Button != null && item.Button.Confirmable
                ? "available"
                : "unavailable";
        }

        private static string SpecialRequirement(UpgradeSystem.Type type)
        {
            switch (type)
            {
                case UpgradeSystem.Type.Ritual_BecomeDisciple:
                    return "unavailable, requires an eligible level 10 follower who is not a disciple";
                case UpgradeSystem.Type.Ritual_Divorce:
                    return "unavailable, requires an eligible follower married to the leader";
                case UpgradeSystem.Type.Ritual_Snowman:
                    return "unavailable, requires a built snowman";
                case UpgradeSystem.Type.Ritual_RanchMeat:
                case UpgradeSystem.Type.Ritual_RanchHarvest:
                    return "unavailable, requires a built ranch";
                default:
                    return "unavailable, current requirements not met";
            }
        }

        internal static bool CanChoose(RitualItem item) =>
            item != null && !item.Locked && !item.Maxed && !item.Cooldown &&
            item.Button != null && item.Button.Confirmable;

        private static void AddRitualCosts(List<string> parts, RitualItem item)
        {
            if (item.Free)
            {
                parts.Add("free ritual");
                return;
            }

            var costs = UpgradeSystem.GetCost(item.RitualType);
            if (costs == null || costs.Count == 0)
            {
                parts.Add("no resource cost");
                return;
            }

            foreach (var cost in costs)
            {
                var owned = Inventory.GetItemQuantity(cost.CostItem);
                parts.Add(ProgressionText.Cost(
                    ItemName(cost.CostItem), cost.CostValue, owned));
            }
        }

        private static void AddRitualEffects(
            List<string> parts, UpgradeSystem.Type type)
        {
            var sin = UpgradeSystem.GetRitualSinChange(type);
            var sinRitual = type == UpgradeSystem.Type.Ritual_Nudism ||
                            type == UpgradeSystem.Type.Ritual_Purge ||
                            type == UpgradeSystem.Type.Ritual_AtoneSin;
            if (sinRitual && !DataManager.Instance.HasPerformedPleasureRitual) sin = 65;

            if (sinRitual)
            {
                if (sin != 0) parts.Add($"sin {Signed(sin)}");
            }
            else
            {
                var faith = UpgradeSystem.GetRitualFaithChange(type);
                if (!Mathf.Approximately(faith, 0f)) parts.Add($"faith {Signed(faith)}");
            }

            var warmth = UpgradeSystem.GetRitualWarmthChange(type);
            if (!Mathf.Approximately(warmth, 0f))
                parts.Add($"winter warmth {Signed(warmth)}");
        }

        private static void AddRitualPosition(List<string> parts, RitualItem item)
        {
            var menu = item.GetComponentInParent<UIRitualsMenuController>();
            var items = menu?.GetComponentsInChildren<RitualItem>(false);
            var index = items == null ? -1 : Array.IndexOf(items, item);
            if (index >= 0)
                parts.Add(ProgressionText.Position("ritual", index, items.Length));
        }

        private static int DoctrineLevel(
            DoctrineUpgradeSystem.DoctrineType type, SermonCategory category)
        {
            for (var level = 1; level <= 4; level++)
            {
                if (DoctrineUpgradeSystem.GetSermonReward(category, level, true) == type ||
                    DoctrineUpgradeSystem.GetSermonReward(category, level, false) == type)
                    return level;
            }
            return 0;
        }

        private static string DoctrineConfirmationInstruction()
        {
            var accept = Key(38, "confirm");
            var action = SettingsManager.Settings.Accessibility.HoldActions
                ? $"Keep {accept} held for 3 seconds to declare"
                : $"Press {accept} to declare";
            return $"{action}. {Key(39, "back")} cancels";
        }

        private static string UpgradeName(UpgradeSystem.Type type)
        {
            var name = Clean(UpgradeSystem.GetLocalizedName(type));
            return name.Length > 0 ? name : RichText.Humanise(type.ToString());
        }

        private static string DoctrineName(DoctrineUpgradeSystem.DoctrineType type)
        {
            var name = Clean(DoctrineUpgradeSystem.GetLocalizedName(type));
            return name.Length > 0 ? name : RichText.Humanise(type.ToString());
        }

        private static string ItemName(InventoryItem.ITEM_TYPE type)
        {
            var name = Clean(InventoryItem.LocalizedName(type));
            return RichText.IsUsableLocalization(name, $"Inventory/{type}")
                ? name
                : RichText.Humanise(type.ToString());
        }

        private static string Signed(float value) =>
            value > 0f ? $"plus {Mathf.Abs(value):0.#}" : $"minus {Mathf.Abs(value):0.#}";

        private static string Clean(string value) => RichText.Clean(value);

        private static string Key(int action, string fallback)
        {
            var key = BindingDump.KeyboardBindingsForAction(action);
            return key.Length > 0 ? key : fallback;
        }
    }

    [HarmonyPatch]
    internal static class RitualDoctrinePatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(RitualItem), "OnRitualItemClicked")]
        private static void BeforeRitualSelected(RitualItem __instance, out bool __state) =>
            __state = RitualDoctrineDescriber.CanChoose(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RitualItem), "OnRitualItemClicked")]
        private static void AfterRitualSelected(RitualItem __instance, bool __state)
        {
            if (__state) RitualDoctrineDescriber.AnnounceRitualSelected(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIChoiceInfoCard<DoctrineResponse>),
            nameof(UIChoiceInfoCard<DoctrineResponse>.PerformHoldAction))]
        private static void BeforeDoctrineConfirmation(
            UIChoiceInfoCard<DoctrineResponse> __instance)
        {
            if (__instance is UIDoctrineChoiceInfoBox box)
                RitualDoctrineDescriber.AnnounceDoctrineConfirmation(box);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DoctrineUpgradeSystem), nameof(DoctrineUpgradeSystem.UnlockAbility))]
        private static void AfterDoctrineUnlocked(
            DoctrineUpgradeSystem.DoctrineType __0, bool __result)
        {
            if (!__result || !Plugin.SpeechEnabled.Value) return;
            RitualDoctrineDescriber.DoctrineUnlocked();
            var name = RichText.Clean(DoctrineUpgradeSystem.GetLocalizedName(__0));
            if (name.Length == 0) name = RichText.Humanise(__0.ToString());
            Speaker.Say($"Doctrine declared. {name}", SpeechPriority.Now);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIMenuControlPrompts), nameof(UIMenuControlPrompts.ShowAcceptButton))]
        private static void AfterAcceptPromptShown(UIMenuControlPrompts __instance) =>
            RitualDoctrineDescriber.OnAcceptPromptShown(__instance);
    }
}
