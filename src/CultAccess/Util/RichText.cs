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

            // The baseline is not overwritten. The game reuses one sprite for more than one
            // item — icon_wood is claimed by both LOG and FORGE_FLAME — and registering in
            // enum order let the second silently replace the first, so lumber costs read as
            // "Sacred Flame". These six were each confirmed against live UI text, which is
            // exactly the evidence the game's table cannot supply for an ambiguous icon.
            if (DefaultSpriteWords.TryGetValue(spriteName, out var baseline))
            {
                // Counted like any other refusal. This is the case that produced the defect,
                // so it is the one most worth being visible in the log.
                if (baseline != clean)
                {
                    LastCollision = $"{spriteName}: kept baseline \"{baseline}\", ignored \"{clean}\"";
                    CollisionCount++;
                }

                return;
            }

            // Between two entries from the game's own table there is no such evidence, so the
            // first is kept and the clash is reported rather than resolved silently.
            if (SpriteWords.TryGetValue(spriteName, out var existing))
            {
                if (existing != clean)
                {
                    LastCollision = $"{spriteName}: kept \"{existing}\", ignored \"{clean}\"";
                    CollisionCount++;
                }

                return;
            }

            SpriteWords[spriteName] = clean;
        }

        /// <summary>How many registrations were refused because the sprite was already claimed.</summary>
        public static int CollisionCount { get; private set; }

        /// <summary>The most recent clash, for the diagnostic that reports the build.</summary>
        public static string LastCollision { get; private set; }

        /// <summary>Drop every registered mapping and return to the baseline.</summary>
        public static void ResetSpriteWords()
        {
            SpriteWords.Clear();
            foreach (var pair in DefaultSpriteWords) SpriteWords[pair.Key] = pair.Value;
            CollisionCount = 0;
            LastCollision = null;
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

            // The game uses a pipe as a visual separator between two facts on one line, and
            // sometimes leaves one dangling where a stripped icon used to be. Measured
            // 2026-08-26: 35 spoken lines carried one, from "Re-Assign |, gozer Lives Here"
            // to "Role: Devout Worker | Age: 20 Days". A screen reader says "vertical bar".
            // Turned into a comma so the existing run-collapsing below removes the dangling
            // ones and keeps the genuine separations as pauses.
            s = s.Replace('|', ',');

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

            if (string.IsNullOrEmpty(expectedTerm)) return true;

            var term = Clean(expectedTerm);
            if (string.Equals(clean, term, System.StringComparison.OrdinalIgnoreCase))
                return false;

            // The term echoed back without its path is the other way a lookup says "no
            // translation", and comparing against the whole term misses it: asked for
            // "Structures/COLLECTED_RESOURCES_CHEST" the reply is the bare key, which is not
            // equal to the term and so passed as a translation.
            //
            // Guarded on the reply still looking like an identifier, because the same shape
            // is a perfectly good answer when it is not one: "Items/Meat" really does
            // translate to "Meat", and rejecting that would lose a correct name.
            var separator = term.LastIndexOf('/');
            if (separator >= 0 &&
                string.Equals(
                    clean,
                    term.Substring(separator + 1),
                    System.StringComparison.OrdinalIgnoreCase) &&
                LooksLikeIdentifier(clean))
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

        /// <summary>
        /// <see cref="Humanise"/> for something known to be an untranslated key, which may
        /// additionally be shouted: "COLLECTED_RESOURCES_CHEST" becomes "Collected Resources
        /// Chest" rather than "COLLECTED RESOURCES CHEST".
        ///
        /// Deliberately not folded into <see cref="Humanise"/>, and the offline harness is why.
        /// Doing it there title-cased a follower called PETERI into "Peteri" — the player named
        /// them that, in caps, on purpose. Casing can only be corrected where the caller knows
        /// the string is a key rather than a name, and the caller that knows is the one that
        /// has just failed to find a translation for it.
        ///
        /// Only when nothing in it is lower case already, so "Hub1_Swamp" and "Shrine II" are
        /// untouched. A lone genuine acronym would become "Hp"; no key in this game is one, and
        /// a mis-cased acronym is a smaller harm than a shouted key.
        /// </summary>
        public static string HumaniseKey(string identifier)
        {
            var humanised = Humanise(identifier);
            return LooksShouted(humanised) ? TitleCase(humanised) : humanised;
        }

        /// <summary>All the letters are upper case and there is more than one of them.</summary>
        private static bool LooksShouted(string text)
        {
            var letters = 0;
            foreach (var character in text)
            {
                if (!char.IsLetter(character)) continue;
                if (char.IsLower(character)) return false;
                letters++;
            }

            return letters > 1;
        }

        /// <summary>Untranslated keys look like this even after their separators are gone.</summary>
        private static bool LooksLikeIdentifier(string text) =>
            text.IndexOf('_') >= 0 || LooksShouted(text);

        private static string TitleCase(string text)
        {
            var builder = new System.Text.StringBuilder(text.Length);
            var startOfWord = true;

            foreach (var character in text)
            {
                if (!char.IsLetter(character))
                {
                    builder.Append(character);
                    startOfWord = true;
                    continue;
                }

                builder.Append(startOfWord
                    ? char.ToUpperInvariant(character)
                    : char.ToLowerInvariant(character));
                startOfWord = false;
            }

            return builder.ToString();
        }

        /// <summary>
        /// Whether <paramref name="needle"/> appears in <paramref name="haystack"/> as whole
        /// words rather than as any substring.
        ///
        /// The distinction is the whole point: "Cook" is a substring of "Cooking Fire", so a
        /// plain containment test would throw away the action and leave the player with a
        /// building and no verb. It is not a whole word there, and it is in "Meal Bad Meat".
        /// </summary>
        public static bool ContainsWord(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return false;

            var index = 0;
            while ((index = haystack.IndexOf(
                       needle, index, System.StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var startsCleanly = index == 0 || !char.IsLetterOrDigit(haystack[index - 1]);
                var end = index + needle.Length;
                var endsCleanly = end >= haystack.Length || !char.IsLetterOrDigit(haystack[end]);
                if (startsCleanly && endsCleanly) return true;

                index++;
            }

            return false;
        }

        /// <summary>
        /// Drop trailing separators and sentence punctuation from a label.
        ///
        /// Two observed defects, one fix. A label that is really a description ends in a full
        /// stop, and the sentences these are composed into add their own, so the player heard
        /// "Guiding to the chest.." — and one game label ends in a bare pipe, which is layout
        /// debris rather than text. Anything ending in a letter, digit or bracket is untouched.
        /// </summary>
        public static string TrimTrailingPunctuation(string text) =>
            string.IsNullOrEmpty(text)
                ? text
                : text.TrimEnd(' ', '	', '.', ',', ';', ':', '|', '-', '/');
    }
}
