using System;
using System.Collections.Generic;

namespace CultAccess.UI
{
    public enum ConfigItemKind
    {
        Submenu,
        Toggle,
        Slider,
        Choice,

        /// <summary>Does something when activated rather than holding a value.</summary>
        Action,
    }

    /// <summary>What activating an item did, so the caller knows what to announce.</summary>
    public enum ConfigActivation
    {
        None,
        EnteredSubmenu,
        Changed,
        Invoked,
    }

    /// <summary>
    /// One row in the mod's own settings menu.
    ///
    /// Deliberately holds delegates rather than values. The real settings live in the BepInEx
    /// config entries, which is what persists them to disk and what already notifies every
    /// subsystem when something changes; a menu that kept its own copy would be a second
    /// source of truth and would drift from the config file the moment either was edited.
    /// Reading and writing through delegates also keeps this file free of any dependency on
    /// BepInEx or Unity, so the navigation and wording can be tested without the game.
    /// </summary>
    public class ConfigItem
    {
        public string Label;

        /// <summary>Spoken on request rather than on focus. Detail is opt-in.</summary>
        public string Description;

        public ConfigItemKind Kind;
        public ConfigPage Submenu;

        public Func<bool> GetToggle;
        public Action<bool> SetToggle;

        public Func<float> GetValue;
        public Action<float> SetValue;
        public float Minimum;
        public float Maximum;
        public float Step;

        /// <summary>Formats the live value for speech, so units belong to the item.</summary>
        public Func<float, string> FormatValue;

        public Func<int> GetChoice;
        public Action<int> SetChoice;
        public string[] Choices;

        public Action Activate;

        /// <summary>
        /// Run when focus lands on this row. The learn-sounds pages use it to play the cue
        /// being described, so moving through the list is itself the lesson.
        /// </summary>
        public Action OnFocus;

        /// <summary>
        /// True when the row's value is currently overridden by something switched off above
        /// it, so the menu can say so instead of letting the player change a dead setting and
        /// wonder why nothing happened.
        /// </summary>
        public Func<bool> Suppressed;
    }

    public class ConfigPage
    {
        public string Title;
        public readonly List<ConfigItem> Items = new List<ConfigItem>();
        public ConfigPage Parent;

        /// <summary>Focus is remembered per page, so backing out and in returns you where you were.</summary>
        public int Index;

        public ConfigPage(string title)
        {
            Title = title;
        }

        public ConfigItem Current =>
            Index >= 0 && Index < Items.Count ? Items[Index] : null;

        public ConfigPage Add(ConfigItem item)
        {
            Items.Add(item);
            return this;
        }
    }

    /// <summary>
    /// Cursor movement and value editing for the settings menu, with no knowledge of speech,
    /// input or the game.
    /// </summary>
    public class ConfigMenuNavigator
    {
        public ConfigPage Page { get; private set; }

        public bool IsOpen => Page != null;

        public ConfigItem Current => Page?.Current;

        public void Open(ConfigPage root)
        {
            Page = root;
            if (Page != null && Page.Index >= Page.Items.Count) Page.Index = 0;
        }

        public void Close() => Page = null;

        /// <summary>
        /// Move the cursor, wrapping at both ends. Wrapping rather than stopping because a
        /// list read aloud has no visible extent: hitting an invisible wall is confusing,
        /// while arriving back at the first item is self-explanatory.
        /// </summary>
        public bool Move(int delta)
        {
            if (Page == null || Page.Items.Count == 0) return false;

            var count = Page.Items.Count;
            Page.Index = ((Page.Index + delta) % count + count) % count;
            return true;
        }

        public bool MoveTo(int index)
        {
            if (Page == null || index < 0 || index >= Page.Items.Count) return false;

            Page.Index = index;
            return true;
        }

        /// <summary>Change the focused item's value. False when it has none to change.</summary>
        public bool Adjust(int direction)
        {
            var item = Current;
            if (item == null || direction == 0) return false;

            switch (item.Kind)
            {
                case ConfigItemKind.Toggle:
                    if (item.GetToggle == null || item.SetToggle == null) return false;
                    // Left is off and right is on, rather than both flipping. A directional
                    // key that toggles makes the state depend on how many times it was
                    // pressed, which is exactly what a player who cannot see it must not
                    // have to track.
                    var wanted = direction > 0;
                    if (item.GetToggle() == wanted) return false;
                    item.SetToggle(wanted);
                    return true;

                case ConfigItemKind.Slider:
                    if (item.GetValue == null || item.SetValue == null) return false;
                    var current = item.GetValue();
                    var step = item.Step <= 0f ? 0.05f : item.Step;
                    var next = Clamp(current + step * direction, item.Minimum, item.Maximum);
                    if (Math.Abs(next - current) < step * 0.001f) return false;
                    item.SetValue(next);
                    return true;

                case ConfigItemKind.Choice:
                    if (item.GetChoice == null || item.SetChoice == null ||
                        item.Choices == null || item.Choices.Length == 0)
                        return false;
                    var count = item.Choices.Length;
                    var index = ((item.GetChoice() + direction) % count + count) % count;
                    if (index == item.GetChoice()) return false;
                    item.SetChoice(index);
                    return true;

                default:
                    return false;
            }
        }

        public ConfigActivation Activate()
        {
            var item = Current;
            if (item == null) return ConfigActivation.None;

            switch (item.Kind)
            {
                case ConfigItemKind.Submenu:
                    if (item.Submenu == null) return ConfigActivation.None;
                    item.Submenu.Parent = Page;
                    Page = item.Submenu;
                    if (Page.Index >= Page.Items.Count) Page.Index = 0;
                    return ConfigActivation.EnteredSubmenu;

                case ConfigItemKind.Toggle:
                    if (item.GetToggle == null || item.SetToggle == null)
                        return ConfigActivation.None;
                    item.SetToggle(!item.GetToggle());
                    return ConfigActivation.Changed;

                case ConfigItemKind.Action:
                    if (item.Activate == null) return ConfigActivation.None;
                    item.Activate();
                    return ConfigActivation.Invoked;

                default:
                    return ConfigActivation.None;
            }
        }

        /// <summary>Leave the current page. False at the root, where the caller should close.</summary>
        public bool Back()
        {
            if (Page?.Parent == null) return false;

            Page = Page.Parent;
            return true;
        }

        private static float Clamp(float value, float minimum, float maximum) =>
            value < minimum ? minimum : value > maximum ? maximum : value;
    }
}
