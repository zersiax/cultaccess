using System;
using CultAccess.Diagnostics;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI.KitchenMenu;
using UnityEngine.UI;

namespace CultAccess.UI
{
    /// <summary>
    /// Makes the cooking bar and tailor timing wheel observable. The game's Auto Cook
    /// and Auto Craft settings remain player-controlled; when they are off, a short
    /// non-verbal cue marks the same success region the visual tracker enters.
    /// </summary>
    [HarmonyPatch]
    internal static class MinigameAnnouncer
    {
        private sealed class ResultState
        {
            public string ItemName;
        }

        /// <summary>
        /// Included in the first focus utterance so a player learns the timing mode before
        /// activating Cook/Craft and still has the option to leave for Accessibility settings.
        /// </summary>
        internal static bool TryDescribeMenuContext(
            Selectable selectable, out string description)
        {
            description = null;
            if (selectable == null) return false;

            if (selectable.GetComponentInParent<UICookingFireMenuController>() != null)
            {
                description = SettingsManager.Settings.Accessibility.AutoCook
                    ? "Cooking menu. Auto Cook is on. Starting the queue completes the timing challenge automatically"
                    : $"Cooking menu. Auto Cook is off. Starting the queue opens a timing challenge. " +
                      $"A rising cue marks the success window; press {InteractKey()} on the cue. " +
                      "Auto Cook can be enabled in Settings, Accessibility";
                return true;
            }

            if (selectable.GetComponentInParent<UITailorMenuController>() != null)
            {
                description = SettingsManager.Settings.Accessibility.AutoCraft
                    ? "Tailor menu. Auto Craft is on. Starting the queue completes the timing wheel automatically"
                    : $"Tailor menu. Auto Craft is off. Starting the queue opens a timing wheel. " +
                      $"A rising cue marks each target; press {InteractKey()} on the cue. " +
                      "Auto Craft can be enabled in Settings, Accessibility";
                return true;
            }

            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(UICookingMinigameOverlayController),
            nameof(UICookingMinigameOverlayController.Initialise))]
        private static void AfterCookingInitialised(
            UICookingMinigameOverlayController __instance, StructuresData StructureInfo)
        {
            if (!Plugin.SpeechEnabled.Value || StructureInfo?.QueuedMeals == null ||
                StructureInfo.QueuedMeals.Count == 0)
                return;

            var count = StructureInfo.QueuedMeals.Count;
            var current = CookingName(StructureInfo.QueuedMeals[0].MealType);
            var mode = SettingsManager.Settings.Accessibility.AutoCook
                ? "Auto Cook is on; timing will complete automatically"
                : $"Auto Cook is off. A rising cue marks the success window; " +
                  $"press {InteractKey()} on the cue";

            var spoken = $"Cooking started. {Count(count, "meal")}. Current, {current}. {mode}.";
            Plugin.Log.LogInfo(
                $"[cooking] started count={count} auto={SettingsManager.Settings.Accessibility.AutoCook} " +
                $"item=\"{current}\"");
            Speaker.Say(spoken, SpeechPriority.Now);
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(UITailorMinigameOverlayController),
            nameof(UITailorMinigameOverlayController.Initialise))]
        private static void AfterTailoringInitialised(
            UITailorMinigameOverlayController __instance, StructuresData StructureInfo)
        {
            if (!Plugin.SpeechEnabled.Value || StructureInfo?.QueuedClothings == null ||
                StructureInfo.QueuedClothings.Count == 0)
                return;

            var count = StructureInfo.QueuedClothings.Count;
            var current = ClothingName(StructureInfo.QueuedClothings[0].ClothingType);
            var mode = SettingsManager.Settings.Accessibility.AutoCraft
                ? "Auto Craft is on; timing targets will complete automatically"
                : $"Auto Craft is off. A rising cue marks each target; " +
                  $"press {InteractKey()} on the cue";

            var spoken = $"Crafting started. {Count(count, "item")}. Current, {current}. {mode}.";
            Plugin.Log.LogInfo(
                $"[crafting] started count={count} auto={SettingsManager.Settings.Accessibility.AutoCraft} " +
                $"item=\"{current}\"");
            Speaker.Say(spoken, SpeechPriority.Now);
        }

        // --- Authoritative result hooks ----------------------------------------------

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Interaction_Kitchen), "OnCook")]
        private static void BeforeCookSuccess(
            Interaction_Kitchen __instance, out ResultState __state)
            => __state = CaptureCookingResult(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Interaction_Kitchen), "OnCook")]
        private static void AfterCookSuccess(
            Interaction_Kitchen __instance, ResultState __state)
            => AnnounceCookingResult(
                __instance, $"Cooked {__state?.ItemName ?? "meal"}");

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Interaction_Kitchen), "OnUnderCook")]
        private static void BeforeUnderCook(
            Interaction_Kitchen __instance, out ResultState __state)
            => __state = CaptureCookingResult(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Interaction_Kitchen), "OnUnderCook")]
        private static void AfterUnderCook(
            Interaction_Kitchen __instance, ResultState __state)
            => AnnounceCookingResult(
                __instance,
                $"Undercooked {__state?.ItemName ?? "meal"}; produced Burned Meal");

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Interaction_Kitchen), "OnBurn")]
        private static void BeforeBurn(
            Interaction_Kitchen __instance, out ResultState __state)
            => __state = CaptureCookingResult(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Interaction_Kitchen), "OnBurn")]
        private static void AfterBurn(
            Interaction_Kitchen __instance, ResultState __state)
            => AnnounceCookingResult(
                __instance, $"Burned {__state?.ItemName ?? "meal"}; produced Burned Meal");

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Interaction_Tailor), "OnCook")]
        private static void BeforeCraftSuccess(
            Interaction_Tailor __instance, out ResultState __state)
            => __state = CaptureTailorResult(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Interaction_Tailor), "OnCook")]
        private static void AfterCraftSuccess(
            Interaction_Tailor __instance, ResultState __state)
            => AnnounceTailorResult(
                __instance, $"Crafted {__state?.ItemName ?? "clothing"}");

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Interaction_Tailor), "OnBurn")]
        private static void BeforeCraftFailure(
            Interaction_Tailor __instance, out ResultState __state)
            => __state = CaptureTailorResult(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Interaction_Tailor), "OnBurn")]
        private static void AfterCraftFailure(
            Interaction_Tailor __instance, ResultState __state)
            => AnnounceTailorResult(
                __instance,
                $"Crafting failed for {__state?.ItemName ?? "clothing"}; " +
                "half of each material cost, rounded down, was returned");

        private static ResultState CaptureCookingResult(Interaction_Kitchen kitchen)
        {
            try
            {
                var queue = kitchen?.StructureInfo?.QueuedMeals;
                if (queue != null && queue.Count > 0)
                    return new ResultState { ItemName = CookingName(queue[0].MealType) };
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not capture cooking result: {e.Message}");
            }

            return new ResultState { ItemName = "meal" };
        }

        private static ResultState CaptureTailorResult(Interaction_Tailor tailor)
        {
            try
            {
                var queue = tailor?.Brain?.Data?.QueuedClothings;
                if (queue != null && queue.Count > 0)
                    return new ResultState { ItemName = ClothingName(queue[0].ClothingType) };
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not capture crafting result: {e.Message}");
            }

            return new ResultState { ItemName = "clothing" };
        }

        private static void AnnounceCookingResult(
            Interaction_Kitchen kitchen, string result)
        {
            if (!Plugin.SpeechEnabled.Value) return;

            var remaining = CookingRemaining(kitchen);
            Plugin.Log.LogInfo($"[cooking] result=\"{result}\" {remaining.Log}");
            Speaker.Say($"{result}. {remaining.Spoken}.", SpeechPriority.Queued);
        }

        private static void AnnounceTailorResult(
            Interaction_Tailor tailor, string result)
        {
            if (!Plugin.SpeechEnabled.Value) return;

            var remaining = TailorRemaining(tailor);
            Plugin.Log.LogInfo($"[crafting] result=\"{result}\" {remaining.Log}");
            Speaker.Say($"{result}. {remaining.Spoken}.", SpeechPriority.Queued);
        }

        private static (string Spoken, string Log) CookingRemaining(
            Interaction_Kitchen kitchen)
        {
            try
            {
                var queue = kitchen?.StructureInfo?.QueuedMeals;
                var count = queue?.Count ?? 0;
                if (count <= 0) return ("Cooking complete", "remaining=0");

                var next = CookingName(queue[0].MealType);
                return ($"{Count(count, "meal")} remaining. Next, {next}",
                    $"remaining={count} next=\"{next}\"");
            }
            catch
            {
                return ("Meal finished", "remaining=unknown");
            }
        }

        private static (string Spoken, string Log) TailorRemaining(
            Interaction_Tailor tailor)
        {
            try
            {
                var queue = tailor?.Brain?.Data?.QueuedClothings;
                var count = queue?.Count ?? 0;
                if (count <= 0) return ("Crafting complete", "remaining=0");

                var next = ClothingName(queue[0].ClothingType);
                return ($"{Count(count, "item")} remaining. Next, {next}",
                    $"remaining={count} next=\"{next}\"");
            }
            catch
            {
                return ("Item finished", "remaining=unknown");
            }
        }

        private static string CookingName(InventoryItem.ITEM_TYPE type)
        {
            var localized = CookingData.GetLocalizedName(type);
            if (RichText.IsUsableLocalization(localized, $"CookingData/{type}/Name"))
                return RichText.Clean(localized);

            localized = InventoryItem.LocalizedName(type);
            return RichText.IsUsableLocalization(localized, $"Inventory/{type}")
                ? RichText.Clean(localized)
                : RichText.Humanise(type.ToString());
        }

        private static string ClothingName(FollowerClothingType type)
        {
            var localized = TailorManager.LocalizedName(type);
            return RichText.IsUsableLocalization(localized, $"Clothing/{type}/Name")
                ? RichText.Clean(localized)
                : RichText.Humanise(type.ToString());
        }

        private static string InteractKey()
        {
            const int interactAction = 9;
            var key = BindingDump.KeyboardBindingsForAction(interactAction);
            return key.Length > 0 ? key : "Interact";
        }

        private static string Count(int value, string singular)
            => value == 1 ? $"1 {singular}" : $"{value} {singular}s";
    }
}
