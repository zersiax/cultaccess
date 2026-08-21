using System;
using System.Reflection;
using HarmonyLib;
using Lamb.UI;
using MMTools.UIInventory;
using UnityEngine;

namespace CultAccess.UI
{
    [HarmonyPatch]
    internal static class TarotPatches
    {
        private static readonly FieldInfo AttachedMenuField =
            AccessTools.Field(typeof(UIMenuControlPrompts), "_attachedMenu");
        private static bool _reportedAttachedMenuFailure;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UITarotPickUpOverlayController), nameof(UITarotPickUpOverlayController.Show),
            new[] { typeof(TarotCards.TarotCard), typeof(bool) })]
        private static void AfterPickupShow(
            UITarotPickUpOverlayController __instance, TarotCards.TarotCard card) =>
            TarotAnnouncer.PresentPickup(__instance, card);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UITarotChoiceOverlayController), nameof(UITarotChoiceOverlayController.Show),
            new[] { typeof(TarotCards.TarotCard), typeof(TarotCards.TarotCard), typeof(bool) })]
        private static void AfterChoiceShow(
            TarotCards.TarotCard card1, TarotCards.TarotCard card2) =>
            TarotAnnouncer.PresentChoice(card1, card2);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UITarotChoiceOverlayController), "FinalizeSelection")]
        private static void AfterChoiceSelected(TarotCards.TarotCard card) =>
            TarotAnnouncer.SelectChoice(card);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIFleeceTarotRewardOverlayController), nameof(UIFleeceTarotRewardOverlayController.Show),
            new[]
            {
                typeof(TarotCards.TarotCard), typeof(TarotCards.TarotCard),
                typeof(TarotCards.TarotCard), typeof(TarotCards.TarotCard), typeof(bool)
            })]
        private static void AfterRewardsShow(UIFleeceTarotRewardOverlayController __instance) =>
            TarotAnnouncer.PresentRewards(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIFleeceTarotRewardOverlayController), "OnShowCompleted")]
        private static void AfterRewardsShown(UIFleeceTarotRewardOverlayController __instance) =>
            TarotAnnouncer.AnnounceRewards(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UITrinketCards), nameof(UITrinketCards.Play),
            new[] { typeof(TarotCards.TarotCard), typeof(Action), typeof(float) })]
        private static void AfterStandaloneShown(
            TarotCards.TarotCard drawnCard, GameObject __result) =>
            TarotAnnouncer.PresentStandalone(
                __result?.GetComponent<UITrinketCards>(), drawnCard);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIInventoryController), nameof(UIInventoryController.Close))]
        private static void AfterInventoryClosed(UIInventoryController __instance)
        {
            if (__instance is UITrinketCards standalone)
                TarotAnnouncer.CloseStandalone(standalone);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIMenuControlPrompts), nameof(UIMenuControlPrompts.ShowAcceptButton))]
        private static void AfterAcceptPromptShown(UIMenuControlPrompts __instance)
        {
            UITarotCardsMenuController menu = null;
            try
            {
                menu = AttachedMenuField?.GetValue(__instance) as UITarotCardsMenuController;
            }
            catch (Exception e)
            {
                if (!_reportedAttachedMenuFailure)
                {
                    _reportedAttachedMenuFailure = true;
                    Plugin.Log.LogWarning(
                        $"Could not read tarot prompt's attached menu: {e.Message}");
                }
            }

            if (menu == null)
                menu = __instance?.GetComponentInParent<UITarotCardsMenuController>();
            if (menu != null) TarotAnnouncer.AnnounceUnlockReady(menu);
        }
    }
}
