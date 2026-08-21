using System;
using System.Collections.Generic;
using System.Reflection;
using CultAccess.Diagnostics;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;
using Lamb.UI.FollowerInteractionWheel;
using Lamb.UI.SermonWheelOverlay;
using Rewired;
using UnityEngine;

namespace CultAccess.UI
{
    /// <summary>
    /// Reads the game's non-follower radial menus. These wheels select directly from the
    /// live UI axes and never move <c>UINavigatorNew</c> focus, so the ordinary focus reader
    /// cannot observe them.
    /// </summary>
    internal static class RadialMenuAnnouncer
    {
        private const float DeselectionReminderDelay = 0.12f;

        private static readonly FieldInfo WeaponItemsField = AccessTools.Field(
            typeof(UIRadialMenuBase<WeaponWheelItem, EquipmentType>), "_wheelItems");
        private static readonly FieldInfo CurseItemsField = AccessTools.Field(
            typeof(UIRadialMenuBase<CurseWheelItem, EquipmentType>), "_wheelItems");
        private static readonly FieldInfo SermonItemsField = AccessTools.Field(
            typeof(UIRadialMenuBase<SermonWheelCategory, SermonCategory>), "_wheelItems");
        private static readonly FieldInfo CurseTypeField = AccessTools.Field(
            typeof(CurseWheelItem), "_curseType");

        private static UIMenuBase _activeWheel;
        private static UIRadialWheelItem _currentItem;
        private static bool _hadSelection;
        private static float _deselectionReminderAt = float.PositiveInfinity;
        private static bool _reportedReadFailure;

        public static bool BlocksWorldHotkeys
        {
            get
            {
                if (_activeWheel != null && _activeWheel.gameObject.activeInHierarchy)
                    return true;

                if (_activeWheel == null || !_activeWheel.gameObject.activeInHierarchy)
                    Clear();
                return false;
            }
        }

        internal static bool IsSupported(UIMenuBase menu) =>
            menu is UIWeaponWheelController ||
            menu is UICurseWheelController ||
            menu is UISermonWheelController;

        internal static void OnOpened(UIMenuBase menu)
        {
            if (!IsSupported(menu) || menu is UIFollowerInteractionWheelOverlayController)
                return;

            _activeWheel = menu;
            _currentItem = null;
            _hadSelection = false;
            _deselectionReminderAt = float.PositiveInfinity;

            var items = Items(menu);
            var kind = WheelName(menu);
            Plugin.Log.LogInfo($"[radial] event=open kind={kind} choices={items.Count}");
            if (!Plugin.SpeechEnabled.Value) return;

            var count = items.Count == 1 ? "1 option" : $"{items.Count} options";
            Speaker.SayParts(
                SpeechPriority.Now,
                $"{kind}. {count}.",
                Controls(menu));
        }

        internal static void OnSelected(UIRadialWheelItem item)
        {
            if (!Owns(item) || item == _currentItem) return;

            _currentItem = item;
            _hadSelection = true;
            _deselectionReminderAt = float.PositiveInfinity;

            var spoken = Describe(item, includeControl: true);
            Plugin.Log.LogInfo(
                $"[radial] event=selected kind={WheelName(_activeWheel)} " +
                $"item={item.GetType().Name} spoken=\"{Safe(spoken)}\"");
            if (Plugin.SpeechEnabled.Value)
                Speaker.Say(spoken, SpeechPriority.Now);
        }

        internal static void OnDeselected(UIRadialWheelItem item)
        {
            if (!Owns(item) || item != _currentItem) return;
            _currentItem = null;
            _deselectionReminderAt = Time.unscaledTime + DeselectionReminderDelay;
        }

        internal static void Tick()
        {
            if (!BlocksWorldHotkeys || !_hadSelection || _currentItem != null ||
                Time.unscaledTime < _deselectionReminderAt)
                return;

            _hadSelection = false;
            _deselectionReminderAt = float.PositiveInfinity;
            if (Plugin.SpeechEnabled.Value)
                Speaker.Say("No radial option selected", SpeechPriority.Queued);
        }

        internal static bool AnnounceCurrent()
        {
            if (!BlocksWorldHotkeys) return false;
            if (_currentItem == null)
            {
                Speaker.Say($"{WheelName(_activeWheel)}. No option selected. " + Controls(_activeWheel));
                return true;
            }

            Speaker.Say(Describe(_currentItem, includeControl: true), SpeechPriority.Now);
            return true;
        }

        internal static bool AnnounceHelp()
        {
            if (!BlocksWorldHotkeys) return false;
            Speaker.Say($"{WheelName(_activeWheel)}. {Controls(_activeWheel)} " +
                        "F12 repeats the current option.");
            return true;
        }

        internal static void Shutdown() => Clear();

        private static string Describe(UIRadialWheelItem item, bool includeControl)
        {
            var parts = new List<string>();
            var title = Title(item);
            if (title.Length > 0) parts.Add(title);

            bool available;
            try { available = item.IsValidOption() && item.Button != null && item.Button.interactable; }
            catch { available = false; }
            parts.Add(available ? "available" : "unavailable");

            var description = Description(item);
            if (description.Length > 0 && description != title) parts.Add(description);

            var items = Items(_activeWheel);
            var index = items.IndexOf(item);
            if (index >= 0) parts.Add($"option {index + 1} of {items.Count}");

            if (item is CurseWheelItem curse && TryCurseType(curse, out var curseType) &&
                PlayerFarming.Instance != null && PlayerFarming.Instance.currentCurse == curseType)
                parts.Add("currently equipped");

            if (includeControl && available && _activeWheel is UISermonWheelController)
                parts.Add($"keep the direction held and press {Key(38, "confirm")}");

            return string.Join(". ", parts.ToArray());
        }

        private static string Title(UIRadialWheelItem item)
        {
            try
            {
                if (item is CurseWheelItem curse && TryCurseType(curse, out var type))
                {
                    var data = EquipmentManager.GetEquipmentData(type);
                    return CleanOrFallback(data?.GetLocalisedTitle(), type.ToString());
                }

                return RichText.Clean(item.GetTitle());
            }
            catch (Exception e)
            {
                ReportReadFailure(e);
                return RichText.Humanise(item.GetType().Name);
            }
        }

        private static string Description(UIRadialWheelItem item)
        {
            try
            {
                if (item is CurseWheelItem curse && TryCurseType(curse, out var type))
                    return RichText.Clean(
                        EquipmentManager.GetEquipmentData(type)?.GetLocalisedDescription());

                return RichText.Clean(item.GetDescription());
            }
            catch (Exception e)
            {
                ReportReadFailure(e);
                return string.Empty;
            }
        }

        private static bool TryCurseType(CurseWheelItem item, out EquipmentType type)
        {
            type = EquipmentType.None;
            if (item == null || CurseTypeField == null) return false;
            try
            {
                type = (EquipmentType)CurseTypeField.GetValue(item);
                return type != EquipmentType.None;
            }
            catch (Exception e)
            {
                ReportReadFailure(e);
                return false;
            }
        }

        private static List<UIRadialWheelItem> Items(UIMenuBase menu)
        {
            var result = new List<UIRadialWheelItem>();
            try
            {
                System.Collections.IEnumerable source = null;
                if (menu is UIWeaponWheelController)
                    source = WeaponItemsField?.GetValue(menu) as System.Collections.IEnumerable;
                else if (menu is UICurseWheelController)
                    source = CurseItemsField?.GetValue(menu) as System.Collections.IEnumerable;
                else if (menu is UISermonWheelController)
                    source = SermonItemsField?.GetValue(menu) as System.Collections.IEnumerable;

                if (source != null)
                {
                    foreach (var value in source)
                    {
                        if (value is UIRadialWheelItem item && item != null &&
                            item.gameObject.activeInHierarchy && item.Visible())
                            result.Add(item);
                    }
                }
            }
            catch (Exception e)
            {
                ReportReadFailure(e);
            }

            return result;
        }

        private static string Controls(UIMenuBase menu)
        {
            var directions = DirectionKeys();
            var cancel = Key(39, "back");
            if (menu is UISermonWheelController)
                return $"Hold {directions} to highlight. Press {Key(38, "confirm")} while " +
                       $"continuing to hold the direction. {cancel} goes back.";

            return $"Use {directions} to highlight an option. The highlighted option applies " +
                   $"when the wheel closes. {cancel} cancels.";
        }

        private static string DirectionKeys()
        {
            var up = Key(34, "W", Pole.Positive);
            var left = Key(35, "A", Pole.Negative);
            var down = Key(34, "S", Pole.Negative);
            var right = Key(35, "D", Pole.Positive);
            return $"{up}, {left}, {down}, or {right}";
        }

        private static string Key(int action, string fallback, Pole? pole = null)
        {
            var value = BindingDump.KeyboardBindingsForAction(action, pole);
            return value.Length > 0 ? value : fallback;
        }

        private static bool Owns(UIRadialWheelItem item) =>
            BlocksWorldHotkeys && item != null &&
            item.GetComponentInParent<UIMenuBase>() == _activeWheel;

        private static string WheelName(UIMenuBase menu)
        {
            if (menu is UIWeaponWheelController) return "Weapon wheel";
            if (menu is UICurseWheelController) return "Curse wheel";
            if (menu is UISermonWheelController) return "Doctrine category wheel";
            return "Radial menu";
        }

        private static string CleanOrFallback(string value, string fallback)
        {
            var clean = RichText.Clean(value);
            return clean.Length > 0 ? clean : RichText.Humanise(fallback);
        }

        private static void ReportReadFailure(Exception e)
        {
            if (_reportedReadFailure) return;
            _reportedReadFailure = true;
            Plugin.Log.LogWarning($"Could not read radial menu data: {e.Message}");
        }

        private static string Safe(string value) =>
            string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\r", " ").Replace("\n", " ").Replace("\"", "'");

        private static void Clear()
        {
            _activeWheel = null;
            _currentItem = null;
            _hadSelection = false;
            _deselectionReminderAt = float.PositiveInfinity;
        }
    }

    [HarmonyPatch]
    internal static class RadialMenuPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIMenuBase), nameof(UIMenuBase.Show), new[] { typeof(bool) })]
        private static void AfterMenuShow(UIMenuBase __instance) =>
            RadialMenuAnnouncer.OnOpened(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIRadialWheelItem), nameof(UIRadialWheelItem.DoSelected))]
        private static void AfterRadialSelected(UIRadialWheelItem __instance)
        {
            if (!(__instance is UIFollowerWheelInteractionItem))
                RadialMenuAnnouncer.OnSelected(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIRadialWheelItem), nameof(UIRadialWheelItem.DoDeselected))]
        private static void AfterRadialDeselected(UIRadialWheelItem __instance)
        {
            if (!(__instance is UIFollowerWheelInteractionItem))
                RadialMenuAnnouncer.OnDeselected(__instance);
        }
    }
}
