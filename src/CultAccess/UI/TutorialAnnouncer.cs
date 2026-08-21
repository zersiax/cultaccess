using System;
using System.Collections;
using System.Collections.Generic;
using CultAccess.Diagnostics;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using Rewired;
using TMPro;
using src.UI.InfoCards;
using src.UI.Overlays.TutorialOverlay;

namespace CultAccess.UI
{
    /// <summary>
    /// Reads the page that the tutorial card has actually configured. Tutorial overlays
    /// reuse one menu while swapping their body text, so a conventional menu-open reader
    /// can only ever observe the first page.
    /// </summary>
    [HarmonyPatch]
    internal static class TutorialAnnouncer
    {
        private static readonly AccessTools.FieldRef<TutorialInfoCard, TextMeshProUGUI> HeaderRef =
            AccessTools.FieldRefAccess<TutorialInfoCard, TextMeshProUGUI>("_header");

        private static readonly AccessTools.FieldRef<TutorialInfoCard, TextMeshProUGUI> DescriptionRef =
            AccessTools.FieldRefAccess<TutorialInfoCard, TextMeshProUGUI>("_description");

        private static readonly AccessTools.FieldRef<TutorialInfoCard, TextMeshProUGUI> BodyRef =
            AccessTools.FieldRefAccess<TutorialInfoCard, TextMeshProUGUI>("_tutorialBody");

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UITutorialOverlayController), "OnShowCompleted")]
        private static void AfterTutorialShown(UITutorialOverlayController __instance)
        {
            if (__instance == null || !Plugin.SpeechEnabled.Value) return;

            var card = __instance.GetComponentInChildren<TutorialInfoCard>(true);
            Announce(card);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TutorialInfoCard), nameof(TutorialInfoCard.NextPage))]
        private static void AfterNextPageRequested(
            TutorialInfoCard __instance, ref IEnumerator __result)
        {
            if (__instance == null || __result == null) return;
            __result = AnnounceAfterPageChange(__result, __instance, __instance.Page);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TutorialInfoCard), nameof(TutorialInfoCard.PreviousPage))]
        private static void AfterPreviousPageRequested(
            TutorialInfoCard __instance, ref IEnumerator __result)
        {
            if (__instance == null || __result == null) return;
            __result = AnnounceAfterPageChange(__result, __instance, __instance.Page);
        }

        private static IEnumerator AnnounceAfterPageChange(
            IEnumerator original, TutorialInfoCard card, int startingPage)
        {
            try
            {
                while (original.MoveNext()) yield return original.Current;
            }
            finally
            {
                (original as IDisposable)?.Dispose();
            }

            if (card != null && card.Page != startingPage) Announce(card);
        }

        private static void Announce(TutorialInfoCard card)
        {
            if (!Plugin.SpeechEnabled.Value || card == null) return;

            var spoken = DescribeCurrent(card);
            if (spoken.Length == 0) return;

            Plugin.Log.LogInfo(
                $"[tutorial] page={card.Page + 1}/{SafeTotal(card)} spoken=\"{spoken}\"");
            Speaker.Say(spoken, SpeechPriority.Now);
        }

        /// <summary>Describe the live page for both automatic reading and F8 re-reading.</summary>
        internal static string DescribeCurrent(TutorialInfoCard card)
        {
            if (card == null) return string.Empty;

            try
            {
                var page = card.Page;
                var total = SafeTotal(card);
                var header = RichText.Clean(HeaderRef(card)?.text);
                var content = page == 0
                    ? RichText.Clean(DescriptionRef(card)?.text)
                    : RichText.Clean(BodyRef(card)?.text);

                var parts = new List<string> { "Tutorial" };
                if (header.Length > 0) parts.Add(header);
                parts.Add($"page {page + 1} of {total}");
                if (content.Length > 0 && content != header) parts.Add(content);

                var controls = Controls(page, total);
                if (controls.Length > 0) parts.Add(controls);
                return string.Join(". ", parts.ToArray());
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not describe tutorial page: {e.Message}");
                return string.Empty;
            }
        }

        private static string Controls(int page, int total)
        {
            const int horizontalAction = 35;
            var left = BindingDump.KeyboardBindingsForAction(horizontalAction, Pole.Negative);
            var right = BindingDump.KeyboardBindingsForAction(horizontalAction, Pole.Positive);
            if (left.Length == 0) left = "left";
            if (right.Length == 0) right = "right";

            if (total <= 1) return CloseHint();
            if (page <= 0) return $"{right} reads the next page";
            if (page >= total - 1)
                return $"{left} reads the previous page. All pages read. {CloseHint()}";
            return $"{left} previous page, {right} next page";
        }

        private static string CloseHint()
        {
            const int acceptAction = 38;
            const int cancelAction = 39;
            var accept = BindingDump.KeyboardBindingsForAction(acceptAction);
            var cancel = BindingDump.KeyboardBindingsForAction(cancelAction);
            if (accept.Length == 0) accept = "confirm";
            if (cancel.Length == 0) cancel = "back";
            return $"{accept} or {cancel} closes the tutorial";
        }

        private static int SafeTotal(TutorialInfoCard card)
        {
            try { return Math.Max(1, card.NumPages + 1); }
            catch { return 1; }
        }
    }
}
