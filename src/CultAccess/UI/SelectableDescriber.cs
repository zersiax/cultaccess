using System.Collections.Generic;
using System.Text;
using CultAccess.Building;
using CultAccess.Diagnostics;
using CultAccess.Util;
using Lamb.UI;
using Rewired;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CultAccess.UI
{
    public enum Verbosity
    {
        /// <summary>Label only. Fastest to listen to once you know the menus.</summary>
        Low,

        /// <summary>Label plus role, state and position. The sensible default.</summary>
        Normal,

        /// <summary>Adds the GameObject name, for working out what a mystery widget is.</summary>
        Diagnostic,
    }

    /// <summary>
    /// Turns a focused <see cref="Selectable"/> into something worth hearing.
    ///
    /// The game builds compound widgets as a focusable proxy (MMSelectable_Toggle,
    /// MMSelectable_Slider, ...) that holds a reference to the real control living on a
    /// sibling or parent object. So reading the focused object alone gives you a label
    /// with no state; we resolve the proxy to its control and read both.
    /// </summary>
    public static class SelectableDescriber
    {
        public static Verbosity Verbosity = Verbosity.Normal;

        public static string Describe(Selectable selectable) =>
            Describe(selectable, out _);

        /// <summary>
        /// As <see cref="Describe(Selectable)"/>, and reports which adapter claimed the
        /// widget. "generic" means none did and the label came from whatever TMP text
        /// happened to be in the subtree — the failure mode behind icon-only controls that
        /// read as a bare quantity. The name is diagnostic only and never spoken.
        /// </summary>
        public static string Describe(Selectable selectable, out string adapter)
        {
            adapter = "none";
            if (selectable == null) return string.Empty;

            // Adventure-map controls are icon-only. Their meaningful label and locked
            // state live on the parent AdventureMapNode, not in child TMP text.
            adapter = "adventure-map";
            if (AdventureMapDescriber.TryDescribe(selectable, out var mapDescription))
                return mapDescription;

            adapter = "build-menu";
            if (BuildMenuDescriber.TryDescribe(selectable, out var buildDescription))
                return buildDescription;

            // Tarot controls are image-only. Their semantic card data lives on the
            // surrounding card item or choice overlay, not in child text.
            adapter = "tarot";
            if (TarotCardDescriber.TryDescribe(selectable, out var tarotDescription))
                return tarotDescription;

            adapter = "inventory";
            if (InventoryMenuDescriber.TryDescribe(selectable, out var inventoryDescription))
                return inventoryDescription;

            adapter = "quest";
            if (QuestMenuDescriber.TryDescribe(selectable, out var questDescription))
                return questDescription;

            adapter = "ritual-doctrine";
            if (RitualDoctrineDescriber.TryDescribe(selectable, out var progressionDescription))
                return progressionDescription;

            adapter = "upgrade-tree";
            if (UpgradeTreeDescriber.TryDescribe(selectable, out var upgradeDescription))
                return upgradeDescription;

            // Crown abilities and fleeces: tiles with a price and no name of their own.
            adapter = "player-upgrades";
            if (PlayerUpgradesDescriber.TryDescribe(selectable, out var playerUpgradeDescription))
                return playerUpgradeDescription;

            // Follower tiles and the summary screen's thought rows: names and prose are
            // already text, but the loyalty, food and illness bars beside them are fills with
            // no text at all, and so is a thought's faith arrow. Ahead of the indoctrination
            // adapter because the summary screen carries both kinds of tile.
            // Notification history rows lead with a bare faith number; move it behind the
            // sentence that gives it meaning.
            adapter = "cult-history";
            if (CultPageDescriber.TryDescribeHistoryRow(selectable, out var historyDescription))
                return historyDescription;

            adapter = "follower-card";
            if (FollowerCardDescriber.TryDescribe(selectable, out var followerDescription))
                return followerDescription;

            // Form, variant and trait tiles: a rendered character or an icon, and no text at
            // all. Also what Twitch chat votes on, so both sides need the name.
            adapter = "indoctrination";
            if (IndoctrinationDescriber.TryDescribe(selectable, out var indoctrinationDescription))
                return indoctrinationDescription;

            // World map icons: a picture with no text beside it.
            adapter = "world-map";
            if (WorldMapDescriber.TryDescribe(selectable, out var worldMapDescription))
                return worldMapDescription;

            adapter = "generic";
            var parts = new List<string>(4);

            var label = ExtractLabel(selectable);
            if (label.Length > 0) parts.Add(label);

            if (Verbosity != Verbosity.Low)
            {
                var state = DescribeState(selectable);
                if (!string.IsNullOrEmpty(state)) parts.Add(state);

                if (!selectable.IsInteractable()) parts.Add("unavailable");
            }

            if (Verbosity == Verbosity.Diagnostic)
                parts.Add($"[{selectable.GetType().Name} {selectable.gameObject.name}]");

            // A widget with no readable label at all is still worth flagging rather
            // than announcing silence — otherwise focus appears to vanish.
            if (parts.Count == 0)
                parts.Add(RichText.Humanise(selectable.gameObject.name));

            return string.Join(", ", parts.ToArray());
        }

        /// <summary>
        /// The distinct MonoBehaviour type names on the widget and its ancestors, nearest
        /// first. Writing an adapter for an icon-only control means finding the component
        /// that holds its meaning, and this is the shortest route to that name. Diagnostic
        /// only, and capped so a deep canvas cannot produce an unreadable line.
        /// </summary>
        public static string ComponentChain(Selectable selectable, int maxDepth = 4)
        {
            if (selectable == null) return string.Empty;

            var seen = new List<string>(8);
            var node = selectable.transform;
            for (var depth = 0; node != null && depth < maxDepth; depth++, node = node.parent)
            {
                foreach (var component in node.GetComponents<MonoBehaviour>())
                {
                    if (component == null) continue;
                    var name = component.GetType().Name;
                    if (!seen.Contains(name)) seen.Add(name);
                }
            }

            return string.Join(", ", seen.ToArray());
        }

        /// <summary>Role, value and position for the compound controls the game uses.</summary>
        private static string DescribeState(Selectable selectable)
        {
            switch (selectable)
            {
                case MMSelectable_Toggle toggle when toggle.Toggle != null:
                    return $"{(toggle.Toggle.Value ? "on" : "off")}, {ConfirmHint("toggle")}";

                case MMSelectable_HorizontalSelector selector when selector.HorizontalSelector != null:
                    return DescribeChoice(
                        selector.HorizontalSelector.Content,
                        selector.HorizontalSelector.CurrentSelection) + $", {HorizontalHint("change")}";

                case MMSelectable_Slider slider when slider.Slider != null:
                    return $"{DescribeSlider(slider.Slider)}, {HorizontalHint("adjust")}";

                case MMSelectable_Dropdown dropdown when dropdown.Dropdown != null:
                    // MMDropdown keeps its current choice in the label text it renders,
                    // which ExtractLabel already picked up, so only the role is missing.
                    return $"dropdown, {dropdown.Dropdown.Content?.Length ?? 0} options";

                case MMSlider bare:
                    return DescribeSlider(bare);

                case Toggle unityToggle:
                    return unityToggle.isOn ? "on" : "off";

                case Button _:
                    return "button";
            }

            return null;
        }

        private static string DescribeChoice(string[] content, int index)
        {
            if (content == null || content.Length == 0) return null;
            if (index < 0 || index >= content.Length) return null;

            var value = RichText.Clean(content[index]);
            return $"{value}, {index + 1} of {content.Length}";
        }

        private static string DescribeSlider(Slider slider)
        {
            if (slider == null) return "slider";

            // Honour the game's own formatter when it has one, so spoken values match
            // what a sighted player would read off the screen.
            if (slider is MMSlider mm && mm.GetCustomDisplayFormat != null)
            {
                var formatted = RichText.Clean(mm.GetCustomDisplayFormat(slider.value));
                if (formatted.Length > 0) return $"slider, {formatted}";
            }

            var range = slider.maxValue - slider.minValue;
            if (range <= 0f) return "slider";

            var percent = Mathf.RoundToInt((slider.value - slider.minValue) / range * 100f);
            return $"slider, {percent} percent";
        }

        private static string HorizontalHint(string verb)
        {
            const int uiHorizontalAction = 35;
            var left = BindingDump.KeyboardBindingsForAction(uiHorizontalAction, Pole.Negative);
            var right = BindingDump.KeyboardBindingsForAction(uiHorizontalAction, Pole.Positive);

            return left.Length > 0 && right.Length > 0
                ? $"{left} and {right} {verb}"
                : $"left and right {verb}";
        }

        private static string ConfirmHint(string verb)
        {
            const int confirmAction = 38;
            var confirm = BindingDump.KeyboardBindingsForAction(confirmAction);
            return confirm.Length > 0 ? $"{confirm} to {verb}" : $"confirm to {verb}";
        }

        /// <summary>
        /// Collect the widget's own text. Text under a *nested* Selectable belongs to a
        /// different control, so it is skipped — otherwise focusing a row in the build
        /// menu would read out every neighbouring button too.
        /// </summary>
        internal static string ExtractLabel(Selectable selectable)
        {
            var builder = new StringBuilder();

            foreach (var text in selectable.GetComponentsInChildren<TMP_Text>(false))
            {
                if (!OwnedBy(selectable, text.transform)) continue;
                Append(builder, text.text);
            }

            if (builder.Length == 0)
            {
                foreach (var text in selectable.GetComponentsInChildren<Text>(false))
                {
                    if (!OwnedBy(selectable, text.transform)) continue;
                    Append(builder, text.text);
                }
            }

            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string raw)
        {
            var clean = RichText.Clean(raw);
            if (clean.Length == 0) return;
            if (builder.Length > 0) builder.Append(", ");
            builder.Append(clean);
        }

        /// <summary>True if <paramref name="child"/>'s nearest Selectable ancestor is <paramref name="owner"/>.</summary>
        private static bool OwnedBy(Selectable owner, Transform child)
        {
            for (var t = child; t != null; t = t.parent)
            {
                if (t == owner.transform) return true;
                if (t.GetComponent<Selectable>() != null) return false;
            }
            return false;
        }
    }
}
