using System.Collections.Generic;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;
using Lamb.UI.DeathScreen;
using Lamb.UI.Menus.DoctrineChoicesMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using src.UI.InfoCards;
using src.UI.Overlays.TutorialOverlay;

namespace CultAccess.UI
{
    /// <summary>
    /// Reads the prose on a panel — tutorial explanations, popup body copy, info text.
    ///
    /// <see cref="FocusAnnouncer"/> only speaks text belonging to the focused control, so
    /// everything that is not attached to a Selectable is silent: which is exactly where
    /// tutorials put their explanation. This fills that gap.
    ///
    /// Announcing is deferred a moment after <c>Show</c> because menus populate their text
    /// after being shown, and because reading over the open animation is unpleasant. There
    /// is also a hotkey, so a menu the automatic path misses is never a dead end.
    /// </summary>
    [HarmonyPatch]
    public static class PanelReader
    {
        private static UIMenuBase _pending;
        private static float _dueAt;

        /// <summary>Grace period after a menu opens before reading its body text.</summary>
        public static float AnnounceDelaySeconds = 0.2f;

        public static bool AutoReadPanels = true;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIMenuBase), nameof(UIMenuBase.Show), new[] { typeof(bool) })]
        private static void AfterShow(UIMenuBase __instance)
        {
            if (!AutoReadPanels || !Plugin.SpeechEnabled.Value) return;

            // This screen populates its stats and loot over a long animation. Its own
            // structural reader announces at the authoritative Continue-focus point.
            if (__instance is UIDeathScreenOverlayController) return;

            // Tutorial pages swap their content without reopening the menu. Their
            // dedicated reader owns both the initial page and every later page.
            if (__instance is UITutorialOverlayController) return;

            // Tarot screens are icon-driven and often populate only after long reveal
            // animations. Their dedicated reader announces at the authoritative focus,
            // reveal, or accept-ready point instead of scraping an early hidden card.
            if (__instance is UITarotCardsMenuController ||
                __instance is UITarotChoiceOverlayController ||
                __instance is UITarotPickUpOverlayController ||
                __instance is UIFleeceTarotRewardOverlayController)
                return;

            // Doctrine choices and upgrade confirmations populate on animation
            // milestones. Their structural readers announce at those exact points.
            if (__instance is UIDoctrineChoicesMenuController ||
                __instance is UIUpgradeUnlockOverlayControllerBase)
                return;

            // These menus never create navigator focus; their radial reader owns both
            // the opening context and the live highlighted option.
            if (RadialMenuAnnouncer.IsSupported(__instance)) return;

            _pending = __instance;
            _dueAt = Time.unscaledTime + AnnounceDelaySeconds;
        }

        /// <summary>Driven from the plugin's Update; nothing happens until a read is due.</summary>
        internal static void Tick()
        {
            if (_pending == null) return;
            if (Time.unscaledTime < _dueAt) return;

            var menu = _pending;
            _pending = null;

            // The menu may have been closed again inside the grace period.
            if (menu == null || !menu.gameObject.activeInHierarchy) return;

            // Measured 2026-08-25: the player opened the Cult page and never pressed the panel
            // key there, which is the expected behaviour — nothing about that screen suggests
            // it. Auto-read is the path that actually reaches those statistics, and it went
            // straight to the generic collector, so the describer was never consulted. Only
            // this one adapter is tried here, deliberately: the others already behave as
            // intended on this path and widening it would change unrelated panels.
            if (CultPageDescriber.TryDescribePanel(menu, out var cultPage))
            {
                Speaker.Say(cultPage, SpeechPriority.Queued);
                return;
            }

            var body = CollectBodyText(menu);
            if (body.Length == 0) return;

            // Queued: the focus announcement for this menu's default control is more
            // urgent than the prose, and should not be talked over.
            Speaker.Say(body, SpeechPriority.Queued);
        }

        /// <summary>
        /// Re-read a menu after it swaps an internal view without calling Show again.
        /// Twitch raffle and voting overlays use one menu instance for loading, active,
        /// result and error views, so their consequential text otherwise stays silent.
        /// </summary>
        internal static void ReadSoon(UIMenuBase menu, float delaySeconds = -1f)
        {
            if (!AutoReadPanels || !Plugin.SpeechEnabled.Value || menu == null) return;

            _pending = menu;
            _dueAt = Time.unscaledTime +
                     (delaySeconds >= 0f ? delaySeconds : AnnounceDelaySeconds);
        }

        /// <summary>Read the topmost open panel on demand.</summary>
        public static void ReadTopmostPanel()
        {
            if (RadialMenuAnnouncer.AnnounceCurrent()) return;

            var menus = UIMenuBase.ActiveMenus;
            if (menus == null || menus.Count == 0)
            {
                if (TarotAnnouncer.TryReadStandalone(includeLore: true, out var standaloneTarot))
                {
                    Speaker.Say(standaloneTarot);
                    return;
                }

                Speaker.Say("No panel open.");
                return;
            }

            var menu = menus[menus.Count - 1];
            if (menu == null)
            {
                Speaker.Say("No panel open.");
                return;
            }

            if (menu is UIDeathScreenOverlayController deathScreen)
            {
                var summary = DeathScreenAnnouncer.Describe(deathScreen);
                Speaker.Say(summary.Length == 0 ? "No run summary available yet." : summary);
                return;
            }

            if (menu is UITutorialOverlayController tutorial)
            {
                var card = tutorial.GetComponentInChildren<TutorialInfoCard>(true);
                var page = TutorialAnnouncer.DescribeCurrent(card);
                Speaker.Say(page.Length == 0 ? "No tutorial page available yet." : page);
                return;
            }

            if (TarotCardDescriber.TryDescribePanel(menu, out var tarot))
            {
                Speaker.Say(tarot);
                return;
            }

            if (RitualDoctrineDescriber.TryDescribePanel(menu, out var doctrine))
            {
                Speaker.Say(doctrine);
                return;
            }

            // Before the generic collector: the cult pages are numbers and bars that belong to
            // no control, so the fallback would recite a string of digits with nothing to say
            // which is which.
            if (CultPageDescriber.TryDescribePanel(menu, out var cultPage))
            {
                Speaker.Say(cultPage);
                return;
            }

            if (menu is UIUpgradeUnlockOverlayControllerBase upgradeOverlay)
            {
                UpgradeTreeDescriber.AnnounceOverlay(upgradeOverlay);
                return;
            }

            var body = CollectBodyText(menu);
            Speaker.Say(body.Length == 0 ? "No readable text on this panel." : body);
        }

        /// <summary>
        /// Gather text that no control owns. Text under a Selectable is skipped because
        /// focus already reads it, and repeating it here would double every button label.
        /// </summary>
        private static string CollectBodyText(UIMenuBase menu)
        {
            var parts = new List<string>();

            foreach (var text in menu.GetComponentsInChildren<TMP_Text>(false))
            {
                if (IsInsideSelectable(text.transform, menu.transform)) continue;

                var clean = RichText.Clean(text.text);
                if (clean.Length == 0) continue;
                if (parts.Contains(clean)) continue;

                parts.Add(clean);
            }

            return string.Join(", ", parts.ToArray());
        }

        private static bool IsInsideSelectable(Transform child, Transform stopAt)
        {
            for (var t = child; t != null && t != stopAt.parent; t = t.parent)
            {
                if (t.GetComponent<Selectable>() != null) return true;
            }

            return false;
        }
    }
}
