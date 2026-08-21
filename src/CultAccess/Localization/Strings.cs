using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CultAccess.Localization
{
    /// <summary>
    /// The mod's own string catalogue. Every user-facing sentence CultAccess composes should
    /// come from here rather than from a literal at the call site.
    ///
    /// Deliberately free of any game dependency so the pure test harness can exercise the
    /// formatters that use it. Language detection lives in the caller: <see cref="Language"/>
    /// is assigned once at startup from the game's own I2 Localization state, which keeps this
    /// class loadable without Assembly-CSharp.
    ///
    /// Three layers, in order:
    /// 1. an override file for the active language, <c>lang/&lt;code&gt;.txt</c>
    /// 2. the English override file, <c>lang/en.txt</c>
    /// 3. compiled-in English defaults
    ///
    /// The compiled defaults are the guarantee. A missing, partial or corrupt file can only
    /// ever fall back to working English, never to silence — a screen reader user cannot tell
    /// a missing string from a frozen game.
    ///
    /// Note the deliberate split of responsibilities: names of items, places and abilities are
    /// *not* in here. Those come from the game's own localization so the player hears the same
    /// words the wiki and their friends use. This catalogue holds only the mod's own
    /// connective prose.
    /// </summary>
    public static class Strings
    {
        private static readonly Dictionary<string, string> Overrides =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static readonly HashSet<string> ReportedMissing =
            new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Keys supplied by the active language's own file, for coverage reporting.</summary>
        private static readonly HashSet<string> TranslatedKeys =
            new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Metadata a translation may declare about itself. Never spoken and excluded from
        /// coverage, but reported at startup so the provenance of a translation is visible: a
        /// machine-drafted file and a human-reviewed one deserve different trust.
        /// </summary>
        private static readonly Dictionary<string, string> Meta =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private const string MetaPrefix = "_meta.";

        /// <summary>Two-letter code of the language in use, for diagnostics and file choice.</summary>
        public static string Language { get; private set; } = "en";

        /// <summary>
        /// Load overrides for a language. Safe to call with a missing directory: the compiled
        /// defaults remain in force and the mod still speaks English.
        /// </summary>
        public static void Initialize(string pluginDirectory, string languageCode) =>
            Initialize(pluginDirectory, languageCode, null);

        /// <summary>
        /// Load overrides for a language. Both the game's language code and its display name
        /// are accepted because I2 stores the code in an asset rather than in code, so the
        /// exact string it returns cannot be known from the decompile. Files may therefore be
        /// named after either — <c>fr.txt</c> or <c>french.txt</c> — and the shipped set uses
        /// the names, which are enumerable from <c>LanguageUtilities.AllLanguages</c>.
        /// </summary>
        public static void Initialize(
            string pluginDirectory, string languageCode, string languageName)
        {
            Overrides.Clear();
            ReportedMissing.Clear();
            TranslatedKeys.Clear();
            Meta.Clear();
            Language = string.IsNullOrEmpty(languageCode)
                ? (string.IsNullOrEmpty(languageName) ? "en" : Normalize(languageName))
                : languageCode;

            if (string.IsNullOrEmpty(pluginDirectory)) return;

            var directory = Path.Combine(pluginDirectory, "lang");
            if (!Directory.Exists(directory)) return;

            // English first, then the active language on top, so a partial translation falls
            // back sentence by sentence instead of all or nothing.
            Load(Path.Combine(directory, "english.txt"), tracksCoverage: false);
            Load(Path.Combine(directory, "en.txt"), tracksCoverage: false);

            if (IsEnglish(languageCode, languageName)) { ReportCoverage(); return; }

            foreach (var candidate in Candidates(languageCode, languageName))
            {
                var path = Path.Combine(directory, candidate + ".txt");
                if (!File.Exists(path)) continue;

                Language = candidate;
                Load(path, tracksCoverage: true);
                break;
            }

            ReportCoverage();
        }

        private static bool IsEnglish(string code, string name)
        {
            if (!string.IsNullOrEmpty(name))
                return name.StartsWith("English", StringComparison.OrdinalIgnoreCase);
            return string.IsNullOrEmpty(code) ||
                   code.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// File names to try, most specific first: the exact code, the normalized display
        /// name, then the code's base. "fr-CA" therefore finds french-canadian.txt via the
        /// name, and falls back to french.txt rather than to English.
        /// </summary>
        private static List<string> Candidates(string code, string name)
        {
            var candidates = new List<string>();

            void Add(string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                if (!candidates.Contains(value)) candidates.Add(value);
            }

            Add(Normalize(code));
            Add(Normalize(name));

            if (!string.IsNullOrEmpty(code))
            {
                var separator = code.IndexOfAny(new[] { '-', '_' });
                if (separator > 0) Add(Normalize(code.Substring(0, separator)));
            }

            if (!string.IsNullOrEmpty(name))
            {
                var bracket = name.IndexOf('(');
                if (bracket > 0) Add(Normalize(name.Substring(0, bracket)));
            }

            return candidates;
        }

        /// <summary>"Portuguese (Brazil)" becomes portuguese-brazil, a usable file name.</summary>
        internal static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var text = new System.Text.StringBuilder();
            foreach (var c in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) text.Append(c);
                else if (text.Length > 0 && text[text.Length - 1] != '-') text.Append('-');
            }

            return text.ToString().Trim('-');
        }

        /// <summary>
        /// Says how complete the active translation is and where it came from. Without this a
        /// half-finished translation is invisible: the untranslated lines simply come out in
        /// English, and nobody knows which ones still need a native speaker.
        /// </summary>
        private static void ReportCoverage()
        {
            if (string.Equals(Language, "en", StringComparison.OrdinalIgnoreCase)) return;

            var total = TranslatableKeyCount();
            var translated = TranslatedKeys.Count;
            var provenance = Meta.TryGetValue("_meta.status", out var status)
                ? status
                : "provenance not declared";

            Report(
                $"Localization {Language}: {translated} of {total} lines translated, " +
                $"{total - translated} falling back to English ({provenance}).");
        }

        private static int TranslatableKeyCount()
        {
            var total = 0;
            foreach (var key in StringDefaults.English.Keys)
                if (!key.StartsWith(MetaPrefix, StringComparison.Ordinal)) total++;
            return total;
        }

        /// <summary>Metadata a translation declared about itself, or an empty string.</summary>
        public static string MetaValue(string key) =>
            Meta.TryGetValue(key, out var value) ? value : string.Empty;

        /// <summary>
        /// Keys the active language does not translate. These are the lines a native speaker
        /// would need to review, and the reason the catalogue is worth shipping as text.
        /// </summary>
        public static List<string> UntranslatedKeys()
        {
            var missing = new List<string>();
            if (string.Equals(Language, "en", StringComparison.OrdinalIgnoreCase)) return missing;

            foreach (var key in StringDefaults.English.Keys)
            {
                if (key.StartsWith(MetaPrefix, StringComparison.Ordinal)) continue;
                if (!TranslatedKeys.Contains(key)) missing.Add(key);
            }

            missing.Sort(StringComparer.Ordinal);
            return missing;
        }

        /// <summary>A complete sentence or fragment for a descriptive key.</summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (Overrides.TryGetValue(key, out var value)) return value;
            if (StringDefaults.English.TryGetValue(key, out var fallback)) return fallback;

            // Returning the key is ugly but audible. Silence would be indistinguishable from
            // a broken feature, and this names the exact string to add.
            ReportMissing(key);
            return key;
        }

        /// <summary>
        /// A template filled with its arguments. Templates carry the whole sentence with
        /// placeholders so a translator controls word order; never assemble a sentence by
        /// concatenating separately translated pieces.
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            var template = Get(key);
            if (args == null || args.Length == 0) return template;

            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, args);
            }
            catch (FormatException e)
            {
                // A translator's stray brace must not take a feature down.
                Report($"Localization template '{key}' is malformed: {e.Message}");
                return template;
            }
        }

        /// <summary>Chooses between a singular and a plural key by count.</summary>
        public static string Plural(string singularKey, string pluralKey, int count) =>
            count == 1 ? Format(singularKey, count) : Format(pluralKey, count);

        private static void Load(string path, bool tracksCoverage)
        {
            if (!File.Exists(path)) return;

            try
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;

                    var separator = line.IndexOf('=');
                    if (separator <= 0) continue;

                    var key = line.Substring(0, separator).Trim();
                    var value = line.Substring(separator + 1).Trim();
                    if (key.Length == 0) continue;

                    // "\n" in a file means a real line break; nothing else is escaped.
                    if (key.StartsWith(MetaPrefix, StringComparison.Ordinal))
                    {
                        Meta[key] = value;
                        continue;
                    }

                    Overrides[key] = value.Replace("\\n", "\n");

                    if (tracksCoverage) TranslatedKeys.Add(key);

                    if (!StringDefaults.English.ContainsKey(key))
                        Report($"Localization file {Path.GetFileName(path)} defines unknown key '{key}'.");
                }
            }
            catch (Exception e)
            {
                Report($"Could not read localization file {path}: {e.Message}");
            }
        }

        private static void ReportMissing(string key)
        {
            if (ReportedMissing.Add(key))
                Report($"Localization key '{key}' has no default; speaking the key itself.");
        }

        /// <summary>
        /// Routed through a hook so this class stays independent of the plugin's logger and
        /// remains usable from the offline test harness.
        /// </summary>
        public static Action<string> Reporter;

        private static void Report(string message)
        {
            var reporter = Reporter;
            if (reporter != null) reporter(message);
        }
    }
}
