using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CultAccess.Util
{
    /// <summary>
    /// Turns TextMeshPro markup into something worth speaking.
    /// The game colours numbers and inlines resource icons everywhere
    /// (see the <c>.Colour()</c> string extension in Assembly-CSharp), so raw
    /// label text is full of tags a screen reader would read out literally.
    /// </summary>
    public static class RichText
    {
        // <sprite name="Coin">, <sprite="Atlas" name="Coin">, <sprite=3>
        private static readonly Regex SpriteTag =
            new Regex(@"<sprite\b(?<attributes>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SpriteName =
            new Regex(
                @"\bname\s*=\s*(?:""(?<double>[^""]*)""|'(?<single>[^']*)'|(?<bare>[^\s>]+))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // TMP accepts the shorthand colour form <#RRGGBB>, which is not matched by
        // the ordinary alphabetic-tag expression below.
        private static readonly Regex ShorthandColourTag =
            new Regex(@"</?#[0-9a-fA-F]{3,8}>", RegexOptions.Compiled);

        private static readonly Regex AnyTag =
            new Regex(@"<\/?[a-zA-Z][^<>]*>", RegexOptions.Compiled);

        private static readonly Regex Whitespace =
            new Regex(@"\s+", RegexOptions.Compiled);

        private static readonly Regex SemanticToken =
            new Regex(@"[\p{L}\p{N}]+", RegexOptions.Compiled);

        /// <summary>
        /// Runs of commas left behind when whatever sat between two separators is removed —
        /// a line break, or an icon that was the whole of its own label. Collapsing them
        /// matters because a reader pauses at each one, so "Lvl I,,, Member for 18 days" is
        /// heard as a stall rather than as a list.
        /// </summary>
        private static readonly Regex CommaRun =
            new Regex(@"(?:\s*,){2,}", RegexOptions.Compiled);

        private static readonly Regex SpaceBeforePunctuation =
            new Regex(@"\s+([,.;:!?])", RegexOptions.Compiled);

        private static readonly Regex DecorativeSpritePrefix =
            new Regex(@"^(?:icon|img)[_\-\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Glyphs in the Unicode Private Use Area, U+E000 to U+F8FF. The game bakes icon
        /// font characters straight into label text — <c>FontImageNames.IconForCommand</c>
        /// returns 49 of them — and by definition they carry no meaning outside the font
        /// that draws them. A screen reader can only read them as garbage or as nothing, so
        /// they are removed rather than passed through.
        ///
        /// The range is deliberately exact. Neighbouring characters the game also uses, such
        /// as fullwidth digits at U+FF10, are real text and must survive.
        /// </summary>
        private static readonly Regex PrivateUseGlyph =
            new Regex("[\uE000-\uF8FF]", RegexOptions.Compiled);

        /// <summary>
        /// The same glyphs again, but written out as the literal text of an escape sequence
        /// rather than as the character. Some of the game's label data carries icons in this
        /// form: a follower select entry reads as "Sinterklaas - Lvl I, \\uf102,
        /// \\uf623, \\ue074, Member for 18 days" in live logs, and each of those is six
        /// ASCII characters rather than one glyph. The cleaner never saw a character to
        /// remove, and a reader says "backslash u f one zero two" out loud.
        ///
        /// Confirmed literal rather than an artefact of the log file: the same log records
        /// other non-ASCII characters, U+FAC3 among them, unescaped.
        ///
        /// Restricted to the same private-use range as <see cref="PrivateUseGlyph"/>. An
        /// escape naming an ordinary character is not icon debris and is left alone.
        /// </summary>
        private static readonly Regex PrivateUseEscapeText =
            new Regex(
                @"\\u(?:[eE][0-9a-fA-F]{3}|[fF][0-8][0-9a-fA-F]{2})",
                RegexOptions.Compiled);

        /// <summary>
        /// The baseline sprite vocabulary, used until the game's own table is available and
        /// restored by <see cref="ResetSpriteWords"/>.
        ///
        /// Confirmed from live English UI strings in LogOutput.log; each nearby label
        /// established what the corresponding sprite represented. This is guesswork compared
        /// to what the game itself knows, which is why it is only a baseline — see
        /// <see cref="RegisterSpriteWord"/>.
        /// </summary>
        private static readonly Dictionary<string, string> DefaultSpriteWords =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["icon_wood"] = "Lumber",
                ["icon_stone"] = "Stone",
                ["icon_berries"] = "Berry",
                ["icon_Meal"] = "Meal",
                ["icon_Followers"] = "Followers",
                ["icon_blackgold"] = "Gold",
            };

        /// <summary>
        /// Icons the game inlines as sprites, mapped to words. TMP sprite names come
        /// from the sprite asset, so unmapped names fall through to a cleaned-up
        /// version of the name itself rather than vanishing silently.
        /// </summary>
        private static readonly Dictionary<string, string> SpriteWords =
            new Dictionary<string, string>(
                DefaultSpriteWords, System.StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Teach the cleaner what one sprite means. The game holds an authoritative table of
        /// this — <c>FontImageNames.GetIconByType</c> maps every item type to the sprite that
        /// draws it — and the names it yields are already localised, so registering from that
        /// table replaces hand-written English guesses with the game's own vocabulary in the
        /// player's own language.
        ///
        /// Kept as a registration call rather than a lookup because this type is compiled
        /// into the offline test harness and must not reference the game's assemblies.
        /// </summary>
        public static void RegisterSpriteWord(string spriteName, string word)
        {
            if (string.IsNullOrEmpty(spriteName)) return;

            var clean = Clean(word);
            if (clean.Length == 0) return;

            SpriteWords[spriteName] = clean;
        }

        /// <summary>Drop every registered mapping and return to the baseline.</summary>
        public static void ResetSpriteWords()
        {
            SpriteWords.Clear();
            foreach (var pair in DefaultSpriteWords) SpriteWords[pair.Key] = pair.Value;
        }

        /// <summary>How many sprites the cleaner currently knows. Diagnostic only.</summary>
        public static int SpriteWordCount => SpriteWords.Count;

        /// <summary>Strip markup and normalise whitespace. Never returns null.</summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            // Most in-line sprites repeat nearby prose, but action-only labels such as
            // "Chop <wood icon>" use the icon as their noun. Resolve every confirmed
            // icon, then omit it only when equivalent text is already present. This also
            // handles singular/plural pairs such as "meals <Meal icon>".
            var visibleText = SpriteTag.Replace(raw, " ");
            visibleText = ShorthandColourTag.Replace(visibleText, string.Empty);
            visibleText = AnyTag.Replace(visibleText, string.Empty);
            visibleText = PrivateUseGlyph.Replace(visibleText, " ");
            visibleText = PrivateUseEscapeText.Replace(visibleText, " ");

            var s = SpriteTag.Replace(raw, m =>
            {
                var nameMatch = SpriteName.Match(m.Groups["attributes"].Value);
                if (!nameMatch.Success) return " ";

                var name = nameMatch.Groups["double"].Success
                    ? nameMatch.Groups["double"].Value
                    : nameMatch.Groups["single"].Success
                        ? nameMatch.Groups["single"].Value
                        : nameMatch.Groups["bare"].Value;
                if (name.StartsWith("img_", System.StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("img-", System.StringComparison.OrdinalIgnoreCase))
                    return " ";

                if (!SpriteWords.TryGetValue(name, out var word))
                {
                    name = DecorativeSpritePrefix.Replace(name, string.Empty);
                    word = Humanise(name);
                }

                if (word.Length == 0 || ContainsEquivalentText(visibleText, word)) return " ";
                return " " + word + " ";
            });

            s = ShorthandColourTag.Replace(s, string.Empty);
            s = AnyTag.Replace(s, string.Empty);

            // Replaced with a space rather than removed, so an icon sitting between two words
            // does not fuse them together.
            s = PrivateUseGlyph.Replace(s, " ");
            s = PrivateUseEscapeText.Replace(s, " ");

            // Line breaks are structure, not silence: give the reader a pause instead.
            s = s.Replace("\r\n", ", ").Replace('\n', ',').Replace('\r', ',');

            s = Whitespace.Replace(s, " ").Trim();
            s = SpaceBeforePunctuation.Replace(s, "$1");

            s = CommaRun.Replace(s, ",");
            return s.Trim(' ', ',');
        }

        /// <summary>
        /// True when a localisation lookup produced player-facing text rather than an
        /// empty value, the game's missing marker, or the untranslated term itself.
        /// </summary>
        public static bool IsUsableLocalization(string localized, string expectedTerm = null)
        {
            var clean = Clean(localized);
            if (clean.Length == 0) return false;
            if (clean.StartsWith(
                    "MISSING LOCALISATION",
                    System.StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(expectedTerm) &&
                string.Equals(
                    clean,
                    Clean(expectedTerm),
                    System.StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        /// <summary>
        /// True when cleaned text contains something a screen reader can identify as a
        /// word or number. Some icon-only game labels deliberately contain just "." as a
        /// layout placeholder; non-empty checks alone would expose that as the object's name.
        /// </summary>
        public static bool HasSemanticText(string text) =>
            SemanticToken.IsMatch(Clean(text));

        /// <summary>
        /// True when cleaned text still carries angle brackets, meaning a tag was malformed
        /// or truncated rather than fully removed. Observed live: a follower's dialogue
        /// speaker came through as "color&gt;", which is markup debris rather than a name.
        ///
        /// That case is now understood and fixed at its source — the caller was splitting a
        /// term path on the last slash, which for a name the game had wrapped in
        /// <c>&lt;color=yellow&gt;...&lt;/color&gt;</c> landed inside the closing tag. The guard
        /// stays because it did its job: it refused to speak debris and it preserved the raw
        /// string that identified the cause. Callers drop the value and log the original.
        /// </summary>
        public static bool HasMarkupResidue(string cleaned) =>
            !string.IsNullOrEmpty(cleaned) &&
            (cleaned.IndexOf('<') >= 0 || cleaned.IndexOf('>') >= 0);

        private static bool ContainsEquivalentText(string text, string semantic)
        {
            var haystack = NormalizedTokens(text);
            var needle = NormalizedTokens(semantic);
            if (needle.Count == 0 || haystack.Count < needle.Count) return false;

            for (var start = 0; start <= haystack.Count - needle.Count; start++)
            {
                var matches = true;
                for (var offset = 0; offset < needle.Count; offset++)
                {
                    if (haystack[start + offset] == needle[offset]) continue;
                    matches = false;
                    break;
                }

                if (matches) return true;
            }

            return false;
        }

        private static List<string> NormalizedTokens(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            foreach (Match match in SemanticToken.Matches(text))
            {
                var token = match.Value.ToLowerInvariant();
                if (token.EndsWith("ies", System.StringComparison.Ordinal) && token.Length > 3)
                    token = token.Substring(0, token.Length - 3) + "y";
                else if (token.EndsWith("s", System.StringComparison.Ordinal) && token.Length > 1)
                    token = token.Substring(0, token.Length - 1);
                result.Add(token);
            }

            return result;
        }

        /// <summary>Turn sprite/enum style identifiers into speakable words.</summary>
        public static string Humanise(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return string.Empty;

            var s = identifier.Replace('_', ' ').Replace('-', ' ');
            // Split camelCase / PascalCase runs: "BuildSite" -> "Build Site".
            s = Regex.Replace(s, @"(?<=[a-z0-9])(?=[A-Z])", " ");
            return Whitespace.Replace(s, " ").Trim();
        }
    }
}
