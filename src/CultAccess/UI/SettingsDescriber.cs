using CultAccess.Diagnostics;
using CultAccess.Util;
using Lamb.UI;
using Lamb.UI.SettingsMenu;
using UnityEngine.UI;

namespace CultAccess.UI
{
    /// <summary>
    /// Describes the settings tab that owns the focused control. Tab order and labels
    /// are read from the instantiated prefab, while key names come from live Rewired
    /// maps, so this stays correct across localization and remapping.
    /// </summary>
    internal static class SettingsDescriber
    {
        internal static bool TryDescribeContext(Selectable selectable, out string description)
        {
            description = null;
            if (selectable == null) return false;

            var settings = selectable.GetComponentInParent<UISettingsMenuController>();
            if (settings == null) return false;

            var navigator = settings.GetComponentInChildren<SettingsTabNavigatorBase>(true);
            if (navigator == null || navigator._tabs == null || navigator._tabs.Length == 0)
            {
                description = "Settings";
                return true;
            }

            var currentIndex = navigator.CurrentMenuIndex;
            if (currentIndex < 0 || currentIndex >= navigator._tabs.Length)
                currentIndex = FindOwningTab(navigator, selectable);

            var current = currentIndex >= 0 && currentIndex < navigator._tabs.Length
                ? navigator._tabs[currentIndex]
                : null;

            var availableCount = 0;
            var availablePosition = 0;
            for (var i = 0; i < navigator._tabs.Length; i++)
            {
                var tab = navigator._tabs[i];
                if (!IsAvailable(tab)) continue;

                availableCount++;
                if (i == currentIndex) availablePosition = availableCount;
            }

            var tabName = TabName(current);
            var tabPart = availableCount > 0 && availablePosition > 0
                ? $"{tabName} tab, {availablePosition} of {availableCount}"
                : $"{tabName} tab";

            const int pageLeftAction = 43;
            const int pageRightAction = 44;
            var previous = BindingDump.KeyboardBindingsForAction(pageLeftAction);
            var next = BindingDump.KeyboardBindingsForAction(pageRightAction);
            if (previous.Length == 0) previous = "page left";
            if (next.Length == 0) next = "page right";

            description = $"Settings. {tabPart}. {previous} previous tab, {next} next tab";
            return true;
        }

        private static int FindOwningTab(SettingsTabNavigatorBase navigator, Selectable selectable)
        {
            for (var i = 0; i < navigator._tabs.Length; i++)
            {
                var tab = navigator._tabs[i];
                if (tab?.Menu != null && selectable.transform.IsChildOf(tab.Menu.transform))
                    return i;
            }

            return -1;
        }

        private static bool IsAvailable(SettingsTab tab)
        {
            return tab != null && tab.gameObject.activeInHierarchy &&
                   tab.Button != null && tab.Button.interactable;
        }

        private static string TabName(SettingsTab tab)
        {
            if (tab == null) return "Current";

            var label = tab.Button == null ? string.Empty : SelectableDescriber.ExtractLabel(tab.Button);
            if (label.Length > 0) return label;

            if (tab.Menu != null)
                return RichText.Humanise(tab.Menu.GetType().Name);

            return RichText.Humanise(tab.gameObject.name);
        }
    }
}
