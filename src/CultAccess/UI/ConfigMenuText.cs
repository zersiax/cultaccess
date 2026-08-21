using System;
using System.Globalization;
using CultAccess.Localization;

namespace CultAccess.UI
{
    /// <summary>
    /// Every sentence the settings menu speaks.
    ///
    /// Separated from the menu's behaviour so the wording can be asserted offline, which
    /// matters more here than almost anywhere else in the mod: this is the one screen a
    /// player uses while they are still learning what the mod does, and a row that reads
    /// back ambiguously is indistinguishable from a setting that does not work.
    ///
    /// The value always follows the label and precedes the position, so a row read at 450
    /// words a minute still resolves to "what it is, what it is set to, where it is".
    /// </summary>
    public static class ConfigMenuText
    {
        /// <summary>Spoken when a page is entered: its name, its size, then the focused row.</summary>
        public static string PageEntry(ConfigPage page)
        {
            if (page == null) return string.Empty;

            var count = page.Items.Count;
            if (count == 0)
                return Strings.Format("config.page_empty", page.Title);

            var size = count == 1
                ? Strings.Format("config.page_items_one", count)
                : Strings.Format("config.page_items_many", count);

            return $"{page.Title}. {size}. {Focus(page)}";
        }

        /// <summary>Spoken when the cursor lands on a row.</summary>
        public static string Focus(ConfigPage page)
        {
            if (page == null) return string.Empty;

            var item = page.Current;
            if (item == null) return Strings.Format("config.page_empty", page.Title);

            var position = ProgressionText.Position(
                Strings.Get("config.item"), page.Index, page.Items.Count);

            var value = Value(item);
            return value.Length == 0
                ? $"{item.Label}, {position}"
                : $"{item.Label}, {value}, {position}";
        }

        /// <summary>Spoken when a row's value changes: the label and the new value only.</summary>
        public static string Changed(ConfigItem item)
        {
            if (item == null) return string.Empty;

            var value = Value(item);
            return value.Length == 0 ? item.Label : $"{item.Label}, {value}";
        }

        /// <summary>
        /// The live value of a row, in words. Empty for rows that hold no value, so the
        /// caller can leave the clause out rather than say "none".
        /// </summary>
        public static string Value(ConfigItem item)
        {
            if (item == null) return string.Empty;

            switch (item.Kind)
            {
                case ConfigItemKind.Submenu:
                    return Strings.Get("config.submenu");

                case ConfigItemKind.Toggle:
                    if (item.GetToggle == null) return string.Empty;
                    var state = Strings.Get(item.GetToggle() ? "config.on" : "config.off");
                    return Suppressed(item) ? AddSuppressed(state) : state;

                case ConfigItemKind.Slider:
                    if (item.GetValue == null) return string.Empty;
                    var value = item.FormatValue != null
                        ? item.FormatValue(item.GetValue())
                        : Percent(item.GetValue());
                    return Suppressed(item) ? AddSuppressed(value) : value;

                case ConfigItemKind.Choice:
                    if (item.GetChoice == null || item.Choices == null) return string.Empty;
                    var index = item.GetChoice();
                    return index >= 0 && index < item.Choices.Length
                        ? item.Choices[index]
                        : string.Empty;

                default:
                    return string.Empty;
            }
        }

        /// <summary>A fraction from 0 to 1, as a whole percentage.</summary>
        public static string Percent(float fraction) =>
            Strings.Format(
                "config.percent",
                Round(fraction * 100f).ToString(CultureInfo.InvariantCulture));

        public static string Metres(float metres) =>
            Strings.Format("config.metres", Number(metres));

        public static string Seconds(float seconds) =>
            Strings.Format("config.seconds", Number(seconds));

        /// <summary>
        /// The controls, spoken on entry and available again from the help key. Every key is
        /// named rather than implied, because this menu is where a player who is lost goes.
        /// </summary>
        public static string Controls(string closeKey) =>
            Strings.Format("config.controls", closeKey);

        /// <summary>The focused row's explanation, spoken only when asked for.</summary>
        public static string Detail(ConfigItem item)
        {
            if (item == null) return string.Empty;
            if (string.IsNullOrEmpty(item.Description)) return Strings.Get("config.no_detail");

            return $"{item.Label}. {item.Description}";
        }

        private static bool Suppressed(ConfigItem item) =>
            item.Suppressed != null && item.Suppressed();

        private static string AddSuppressed(string value) =>
            $"{value}, {Strings.Get("config.currently_silent")}";

        private static string Number(float value)
        {
            var rounded = Math.Abs(value - Round(value)) < 0.05f
                ? Round(value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.#", CultureInfo.InvariantCulture);
            return rounded;
        }

        private static int Round(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
