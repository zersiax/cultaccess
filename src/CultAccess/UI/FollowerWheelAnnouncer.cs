using System;
using System.Collections.Generic;
using System.Reflection;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;
using Lamb.UI.FollowerInteractionWheel;
using UnityEngine;

namespace CultAccess.UI
{
    /// <summary>
    /// Makes the follower-command radial usable without seeing its icon ring.
    ///
    /// This wheel does not participate in <c>UINavigatorNew</c>: it derives its selection
    /// directly from the live UI axis, clears that selection when the direction is released,
    /// and only accepts while a direction remains held. Consequently the ordinary focus
    /// announcer cannot observe it, and an unqualified "press E" hint is insufficient.
    /// </summary>
    internal static class FollowerWheelAnnouncer
    {
        private const float DeselectionReminderDelay = 0.12f;

        private static UIFollowerInteractionWheelOverlayController _activeWheel;
        private static UIFollowerWheelInteractionItem _currentItem;
        private static Follower _follower;
        private static bool _cancellable;
        private static bool _hadSelection;
        private static float _deselectionReminderAt = float.PositiveInfinity;

        /// <summary>
        /// World-scanner, navigation, and combat hotkeys must not operate behind this modal.
        /// The game deliberately marks the player inactive for the duration of the wheel.
        /// </summary>
        public static bool BlocksWorldHotkeys
        {
            get
            {
                if (_activeWheel != null && _activeWheel.gameObject.activeInHierarchy)
                    return true;

                if (_activeWheel == null) ClearState();
                return false;
            }
        }

        internal static void OnOpened(
            UIFollowerInteractionWheelOverlayController wheel,
            Follower follower,
            List<CommandItem> commands,
            bool cancellable)
        {
            if (wheel == null) return;

            _activeWheel = wheel;
            _currentItem = null;
            _follower = follower;
            _cancellable = cancellable;
            _hadSelection = false;
            _deselectionReminderAt = float.PositiveInfinity;

            Plugin.Log.LogInfo(
                $"[follower wheel] event=open follower='{Safe(FollowerName(follower))}' " +
                $"choices={commands?.Count ?? 0} cancellable={cancellable}");

            // Log-only, and separate from the line above because it answers a different
            // question: which group the game chose for this follower's state, and therefore
            // whether a command that is not here is missing or merely elsewhere.
            Diagnostics.FollowerWheelDiagnostics.LogWheelOpened(follower, commands);

            if (!Plugin.SpeechEnabled.Value) return;

            var owner = FollowerName(follower);
            var heading = string.IsNullOrEmpty(owner)
                ? "Assign follower work."
                : $"Assign work to {owner}.";
            var choices = DescribeChoices(follower, commands);
            var controls = "Hold W, A, S, or D to select. Press E while continuing to hold " +
                           "the direction.";
            var cancel = cancellable ? "F goes back." : "A choice is required.";

            Speaker.SayParts(SpeechPriority.Now, heading, choices, controls, cancel);
        }

        internal static void OnSelected(UIFollowerWheelInteractionItem item)
        {
            if (!Owns(item) || item == _currentItem) return;

            _currentItem = item;
            _hadSelection = true;
            _deselectionReminderAt = float.PositiveInfinity;

            var title = RichText.Clean(item.GetTitle());
            var description = RichText.Clean(item.GetDescription());
            var availability = IsAvailable(item) ? null : "Unavailable";

            Plugin.Log.LogInfo(
                $"[follower wheel] event=selected command={item.FollowerCommand} " +
                $"title='{Safe(title)}' available={availability == null}");

            if (Plugin.SpeechEnabled.Value)
                Speaker.SayParts(SpeechPriority.Now, title, availability, description);
        }

        internal static void OnDeselected(UIFollowerWheelInteractionItem item)
        {
            if (!Owns(item) || item != _currentItem) return;

            _currentItem = null;
            _deselectionReminderAt = Time.unscaledTime + DeselectionReminderDelay;
        }

        /// <summary>
        /// A direction change deselects one item and selects another in the same frame.
        /// Delay the warning slightly so only releasing the direction, not changing it,
        /// produces the reminder.
        /// </summary>
        public static void Tick()
        {
            if (!BlocksWorldHotkeys || !_hadSelection || _currentItem != null ||
                Time.unscaledTime < _deselectionReminderAt)
                return;

            _hadSelection = false;
            _deselectionReminderAt = float.PositiveInfinity;
            if (Plugin.SpeechEnabled.Value)
                Speaker.Say(
                    "No option selected. Keep a direction held while pressing E.",
                    SpeechPriority.Queued);
        }

        public static void AnnounceCurrent()
        {
            if (!BlocksWorldHotkeys) return;

            if (_currentItem == null)
            {
                Speaker.Say("No option selected. Hold W, A, S, or D, then press E while " +
                            "continuing to hold the direction.");
                return;
            }

            var availability = IsAvailable(_currentItem) ? null : "Unavailable";
            Speaker.SayParts(
                SpeechPriority.Now,
                _currentItem.GetTitle(),
                availability,
                _currentItem.GetDescription(),
                _cancellable ? "F goes back" : "A choice is required");
        }

        public static void AnnounceHelp()
        {
            if (!BlocksWorldHotkeys) return;

            var cancel = _cancellable ? "F goes back." : "This choice cannot be cancelled.";
            Speaker.Say(
                "Follower command wheel. Hold W, A, S, or D to hear and select an option. " +
                "Press E while continuing to hold that direction. F12 repeats the current " +
                $"option. {cancel}");
        }

        internal static void OnClosed(UIFollowerInteractionWheelOverlayController wheel)
        {
            if (wheel != _activeWheel) return;
            Plugin.Log.LogInfo("[follower wheel] event=closed");
            ClearState();
        }

        private static bool Owns(UIFollowerWheelInteractionItem item)
        {
            if (!BlocksWorldHotkeys || item == null) return false;
            return item.GetComponentInParent<UIFollowerInteractionWheelOverlayController>() ==
                   _activeWheel;
        }

        private static bool IsAvailable(UIFollowerWheelInteractionItem item)
        {
            var command = item?.CommandItem;
            return command == null || command.IsAvailable(_follower);
        }

        private static string DescribeChoices(Follower follower, List<CommandItem> commands)
        {
            var count = commands?.Count ?? 0;
            var countText = count == 1 ? "1 choice" : $"{count} choices";
            if (count == 0 || count > 4) return countText + ".";

            var labels = new List<string>(count);
            foreach (var command in commands)
            {
                if (command == null) continue;
                var label = RichText.Clean(command.GetTitle(follower));
                if (label.Length > 0) labels.Add(label);
            }

            return labels.Count == 0
                ? countText + "."
                : $"{countText}: {string.Join(", ", labels.ToArray())}.";
        }

        private static string FollowerName(Follower follower)
        {
            var name = follower?.Brain?.Info?.Name;
            var clean = RichText.Clean(name);
            return clean.Length == 0 ? null : clean;
        }

        private static void ClearState()
        {
            _activeWheel = null;
            _currentItem = null;
            _follower = null;
            _cancellable = false;
            _hadSelection = false;
            _deselectionReminderAt = float.PositiveInfinity;
        }

        private static string Safe(string value) =>
            string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\r", " ").Replace("\n", " ").Replace("'", string.Empty);
    }

    [HarmonyPatch]
    internal static class FollowerWheelPatches
    {
        [HarmonyPatch]
        private static class ShowPatch
        {
            private static MethodBase TargetMethod() =>
                AccessTools.Method(
                    typeof(UIFollowerInteractionWheelOverlayController),
                    nameof(UIFollowerInteractionWheelOverlayController.Show),
                    new[]
                    {
                        typeof(Follower), typeof(List<CommandItem>), typeof(bool),
                        typeof(bool), typeof(PlayerFarming),
                    });

            private static void Postfix(
                UIFollowerInteractionWheelOverlayController __instance,
                Follower __0,
                List<CommandItem> __1,
                bool __3) =>
                FollowerWheelAnnouncer.OnOpened(__instance, __0, __1, __3);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIFollowerWheelInteractionItem), nameof(UIFollowerWheelInteractionItem.DoSelected))]
        private static void AfterSelected(UIFollowerWheelInteractionItem __instance) =>
            FollowerWheelAnnouncer.OnSelected(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIRadialWheelItem), nameof(UIRadialWheelItem.DoDeselected))]
        private static void AfterDeselected(UIRadialWheelItem __instance)
        {
            // UIFollowerWheelInteractionItem inherits this implementation without an
            // override, so Harmony must patch the declaring base type. Other radial item
            // subclasses also pass through here and are deliberately ignored.
            if (__instance is UIFollowerWheelInteractionItem item)
                FollowerWheelAnnouncer.OnDeselected(item);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIFollowerInteractionWheelOverlayController), "OnDestroy")]
        private static void BeforeDestroy(UIFollowerInteractionWheelOverlayController __instance) =>
            FollowerWheelAnnouncer.OnClosed(__instance);
    }
}
