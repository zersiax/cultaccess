using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;
using Lamb.UI.SettingsMenu;
using UnityEngine;
using UnityEngine.UI;
using src.UINavigator;

namespace CultAccess.UI
{
    /// <summary>
    /// Announces UI focus changes.
    ///
    /// Hooks the private <c>UINavigatorNew.NavigateTo</c> rather than the public
    /// <c>OnSelectionChanged</c> event: that event only fires for directional navigation,
    /// so it misses mouse hover and the default selection set when a menu opens.
    /// <c>NavigateTo</c> is the single point where <c>_currentSelectable</c> actually
    /// changes, so patching it catches every focus change from every source.
    /// </summary>
    public static class FocusAnnouncer
    {
        private static readonly AccessTools.FieldRef<UINavigatorNew, IMMSelectable> CurrentSelectableRef =
            AccessTools.FieldRefAccess<UINavigatorNew, IMMSelectable>("_currentSelectable");

        private static Selectable _lastAnnounced;
        private static string _lastMenuName;

        /// <summary>Announce the menu title when focus moves into a different menu.</summary>
        public static bool AnnounceMenuContext = true;

        internal static void OnNavigated(UINavigatorNew navigator)
        {
            if (navigator == null || !Plugin.SpeechEnabled.Value) return;

            Selectable selectable;
            try
            {
                selectable = CurrentSelectableRef(navigator)?.Selectable;
            }
            catch
            {
                // A destroyed proxy can leave a dangling reference mid-transition.
                return;
            }

            if (selectable == null || selectable == _lastAnnounced) return;
            _lastAnnounced = selectable;

            var menu = AnnounceMenuContext ? MenuNameFor(selectable) : null;
            var description = SelectableDescriber.Describe(selectable, out var adapter);

            // Recipe buttons carry only a quantity label, so the generic reader announced a
            // bare number. Replace it before anything else uses the description.
            if (KitchenMenuDescriber.TryDescribeItem(selectable, out var recipeDescription))
            {
                description = recipeDescription;
                adapter = "kitchen";
            }

            LogFocus(selectable, menu, adapter, description);

            // The run-result screen animates its contents for several seconds and only
            // focuses Continue once every title, stat and loot value is final. Include
            // that complete structural summary with the first focus announcement.
            if (DeathScreenAnnouncer.TryDescribeFirst(selectable, out var deathSummary))
            {
                _lastMenuName = menu;
                Speaker.SayParts(SpeechPriority.Now, menu, deathSummary, description);
                return;
            }

            if (menu != null && menu != _lastMenuName)
            {
                _lastMenuName = menu;
                Speaker.SayParts(SpeechPriority.Now, menu, description);
            }
            else
            {
                Speaker.Say(description, SpeechPriority.Now);
            }
        }

        /// <summary>
        /// Evidence for the one path that produced no evidence at all. Focus is the mod's
        /// most-used announcement route and it was the only one that logged nothing, so a
        /// session could not tell a screen that read correctly from a screen that was never
        /// reached. Two live cases turned on exactly that distinction: the cooking recipe
        /// buttons and the doctrine tiles both read as a bare quantity, because they are
        /// icon-only and the generic reader took whatever TMP text was in the subtree.
        ///
        /// <c>adapter=generic</c> is therefore the line to search for: it names every widget
        /// still being read by fallback, and the component chain beside it names the
        /// component an adapter would have to be written against.
        ///
        /// Log only, never spoken, per rules section 2.
        /// </summary>
        private static void LogFocus(
            Selectable selectable, string menu, string adapter, string description)
        {
            if (!Plugin.LogFocus.Value) return;

            Plugin.Log.LogInfo(
                $"[focus] adapter={adapter} type={selectable.GetType().Name} " +
                $"object={Quote(selectable.gameObject.name)} menu={Quote(menu)} " +
                $"interactable={selectable.IsInteractable()} " +
                $"spoken={Quote(description)} " +
                $"components: {SelectableDescriber.ComponentChain(selectable)}");
        }

        private static string Quote(string value) => value == null ? "<null>" : $"\"{value}\"";

        /// <summary>
        /// Best-effort name for the menu that owns this control, so the user gets context
        /// on entering a new screen. Prefers a real title label over the class name.
        /// </summary>
        private static string MenuNameFor(Selectable selectable)
        {
            if (MinigameAnnouncer.TryDescribeMenuContext(selectable, out var minigameContext))
                return minigameContext;

            if (TwitchAnnouncer.TryDescribeSettingsContext(selectable, out var twitchContext))
                return twitchContext;

            if (SettingsDescriber.TryDescribeContext(selectable, out var settingsContext))
                return settingsContext;

            if (TarotCardDescriber.TryDescribeContext(selectable, out var tarotContext))
                return tarotContext;

            if (InventoryMenuDescriber.TryDescribeContext(selectable, out var inventoryContext))
                return inventoryContext;

            if (QuestMenuDescriber.TryDescribeContext(selectable, out var questContext))
                return questContext;

            if (RitualDoctrineDescriber.TryDescribeContext(selectable, out var progressionContext))
                return progressionContext;

            if (UpgradeTreeDescriber.TryDescribeContext(selectable, out var upgradeContext))
                return upgradeContext;

            var menu = selectable.GetComponentInParent<UIMenuBase>();
            if (menu == null) return null;

            if (menu is UIAdventureMapOverlayController adventureMap)
            {
                return adventureMap.Cancellable
                    ? "Choose your next route. Use direction keys to review choices. E accepts. F goes back"
                    : "Choose your next route. Use direction keys to review choices. E accepts";
            }

            // Class names are consistently descriptive here (UISettingsMenuController,
            // UIBuildMenuController), and far more stable than digging for a title object.
            var typeName = menu.GetType().Name;
            typeName = StripAffixes(typeName);

            return RichText.Humanise(typeName);
        }

        private static string StripAffixes(string typeName)
        {
            if (typeName.StartsWith("UI", System.StringComparison.Ordinal) && typeName.Length > 2)
                typeName = typeName.Substring(2);

            const string controller = "Controller";
            if (typeName.EndsWith(controller, System.StringComparison.Ordinal))
                typeName = typeName.Substring(0, typeName.Length - controller.Length);

            return typeName;
        }

        /// <summary>Forget cached focus so the next navigation re-announces in full.</summary>
        internal static void Reset()
        {
            _lastAnnounced = null;
            _lastMenuName = null;
        }

        /// <summary>Re-read whatever currently has focus, on demand.</summary>
        public static void SpeakCurrentFocus()
        {
            var navigator = MonoSingleton<UINavigatorNew>.Instance;
            if (navigator == null)
            {
                Speaker.Say("No menu focus.");
                return;
            }

            var selectable = CurrentSelectableRef(navigator)?.Selectable;
            if (selectable == null)
            {
                Speaker.Say("No menu focus.");
                return;
            }

            Speaker.Say(SelectableDescriber.Describe(selectable), SpeechPriority.Now);
        }
    }

    [HarmonyPatch]
    internal static class UINavigatorPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UINavigatorNew), "NavigateTo")]
        private static void AfterNavigateTo(UINavigatorNew __instance)
        {
            FocusAnnouncer.OnNavigated(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UINavigatorNew), "Clear")]
        private static void AfterClear()
        {
            // Focus was dropped entirely (menu closed); next selection is a fresh context.
            FocusAnnouncer.Reset();
        }
    }
}
