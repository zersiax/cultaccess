using System;
using System.Text.RegularExpressions;
using CultAccess.Util;

namespace CultAccess.Localization
{
    /// <summary>
    /// Teaches <see cref="RichText"/> what the game's inline icons mean, from the game's own
    /// table rather than from guesses.
    ///
    /// <c>FontImageNames.GetIconByType</c> maps every <c>InventoryItem.ITEM_TYPE</c> to the
    /// sprite tag that draws it, and <c>GetIconWhiteByType</c> maps a further set of
    /// light-on-dark variants to the same items. Pairing those with
    /// <c>InventoryItem.LocalizedName</c> yields the complete icon vocabulary **already in
    /// the player's language**, which is something the six hand-written English entries in
    /// <see cref="RichText"/> could never be.
    ///
    /// The defect this generalises: a tarot card read as "Curses consume 25% less Fervour
    /// black Soul", because <c>icon_blackSoul</c> was unmapped and fell through to a
    /// humanised version of the sprite's own name.
    ///
    /// An item whose name does not resolve is deliberately left unregistered. The existing
    /// fallback humanises the sprite name, and "icon_MeatMorsel" becomes a better spoken
    /// word than the enum's "MEAT MORSEL" would be.
    /// </summary>
    internal static class SpriteVocabulary
    {
        private static readonly Regex SpriteTagName = new Regex(
            @"<sprite\b[^>]*\bname\s*=\s*""(?<name>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Rebuild from scratch. Safe to call again when the language changes: the reset
        /// drops the previous language's words rather than leaving them to shadow the new
        /// ones, since the sprite names are language-independent keys.
        /// </summary>
        internal static void Build()
        {
            RichText.ResetSpriteWords();

            var items = 0;
            var registered = 0;
            var unnamed = 0;

            foreach (InventoryItem.ITEM_TYPE type in
                     Enum.GetValues(typeof(InventoryItem.ITEM_TYPE)))
            {
                items++;
                string word;
                try
                {
                    var localized = InventoryItem.LocalizedName(type);
                    if (!RichText.IsUsableLocalization(localized, $"Inventory/{type}"))
                    {
                        unnamed++;
                        continue;
                    }

                    word = RichText.Clean(localized);
                }
                catch (Exception)
                {
                    // A gap in the game's own name table is not a reason to abandon the rest.
                    unnamed++;
                    continue;
                }

                registered += Register(SafeIcon(type, white: false), word);
                registered += Register(SafeIcon(type, white: true), word);
            }

            Plugin.Log.LogInfo(
                $"[sprite vocabulary] built from the game's icon table: {registered} sprite(s) " +
                $"named across {items} item type(s), {unnamed} without a usable name; " +
                $"{RichText.SpriteWordCount} known in total");
        }

        private static string SafeIcon(InventoryItem.ITEM_TYPE type, bool white)
        {
            try
            {
                return white
                    ? FontImageNames.GetIconWhiteByType(type)
                    : FontImageNames.GetIconByType(type);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Both tables return an empty string for a type with no icon, so a miss is ordinary
        /// rather than exceptional.
        /// </summary>
        private static int Register(string spriteTag, string word)
        {
            if (string.IsNullOrEmpty(spriteTag)) return 0;

            var match = SpriteTagName.Match(spriteTag);
            if (!match.Success) return 0;

            RichText.RegisterSpriteWord(match.Groups["name"].Value, word);
            return 1;
        }
    }
}
