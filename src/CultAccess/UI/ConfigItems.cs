using System;
using BepInEx.Configuration;
using CultAccess.Localization;

namespace CultAccess.UI
{
    /// <summary>
    /// Shorthand for building settings rows, and the one place that connects a row to the
    /// BepInEx config entry behind it.
    ///
    /// Writing through the entry rather than to a field is what makes the menu and the config
    /// file the same setting rather than two: the entry persists the value to disk, and its
    /// existing <c>SettingChanged</c> subscribers are what push it into the subsystem that
    /// uses it. A row that set a static field directly would appear to work and would be
    /// forgotten on the next launch.
    ///
    /// Labels and explanations are resolved through the localisation layer here, at the point
    /// the menu is built, which is after the game has settled on its language.
    /// </summary>
    internal static class ConfigItems
    {
        /// <summary>Detail text lives under the label's key with this suffix.</summary>
        private const string DetailSuffix = ".detail";

        internal static ConfigItem Toggle(string key, ConfigEntry<bool> entry) =>
            Toggle(key, () => entry.Value, value => entry.Value = value);

        internal static ConfigItem Toggle(string key, Func<bool> get, Action<bool> set) =>
            new ConfigItem
            {
                Label = Strings.Get(key),
                Description = Strings.Get(key + DetailSuffix),
                Kind = ConfigItemKind.Toggle,
                GetToggle = get,
                SetToggle = set,
            };

        internal static ConfigItem Percent(string key, ConfigEntry<float> entry) =>
            Slider(key, () => entry.Value, value => entry.Value = value,
                0f, 1f, 0.05f, ConfigMenuText.Percent);

        internal static ConfigItem Percent(string key, Func<float> get, Action<float> set) =>
            Slider(key, get, set, 0f, 1f, 0.05f, ConfigMenuText.Percent);

        internal static ConfigItem Metres(
            string key, Func<float> get, Action<float> set, float minimum, float maximum) =>
            Slider(key, get, set, minimum, maximum, 1f, ConfigMenuText.Metres);

        internal static ConfigItem Seconds(
            string key,
            Func<float> get,
            Action<float> set,
            float minimum,
            float maximum,
            float step) =>
            Slider(key, get, set, minimum, maximum, step, ConfigMenuText.Seconds);

        internal static ConfigItem Slider(
            string key,
            Func<float> get,
            Action<float> set,
            float minimum,
            float maximum,
            float step,
            Func<float, string> format) =>
            new ConfigItem
            {
                Label = Strings.Get(key),
                Description = Strings.Get(key + DetailSuffix),
                Kind = ConfigItemKind.Slider,
                GetValue = get,
                SetValue = set,
                Minimum = minimum,
                Maximum = maximum,
                Step = step,
                FormatValue = format,
            };

        internal static ConfigItem Choice(
            string key,
            Func<int> get,
            Action<int> set,
            params string[] choiceKeys)
        {
            var choices = new string[choiceKeys.Length];
            for (var i = 0; i < choiceKeys.Length; i++)
                choices[i] = Strings.Get(choiceKeys[i]);

            return new ConfigItem
            {
                Label = Strings.Get(key),
                Description = Strings.Get(key + DetailSuffix),
                Kind = ConfigItemKind.Choice,
                GetChoice = get,
                SetChoice = set,
                Choices = choices,
            };
        }

        internal static ConfigItem Submenu(string key, ConfigPage page) =>
            new ConfigItem
            {
                Label = Strings.Get(key),
                Description = Strings.Get(key + DetailSuffix),
                Kind = ConfigItemKind.Submenu,
                Submenu = page,
            };

        /// <summary>A submenu whose label is supplied rather than looked up, such as a cue name.</summary>
        internal static ConfigItem Submenu(string label, string description, ConfigPage page) =>
            new ConfigItem
            {
                Label = label,
                Description = description,
                Kind = ConfigItemKind.Submenu,
                Submenu = page,
            };

        internal static ConfigItem Action(string key, Action activate) =>
            new ConfigItem
            {
                Label = Strings.Get(key),
                Description = Strings.Get(key + DetailSuffix),
                Kind = ConfigItemKind.Action,
                Activate = activate,
            };
    }
}
