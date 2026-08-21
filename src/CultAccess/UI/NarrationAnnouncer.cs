using System.Collections;
using System.Reflection;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using I2.Loc;

namespace CultAccess.UI
{
    /// <summary>
    /// Announces narration that is not conversation: the full-screen quote cards used for
    /// the intro and boss interludes, and cutscene subtitles.
    ///
    /// These are separate systems from <c>MMConversation</c> with their own UI, which is why
    /// hooking conversation alone left the entire opening narration silent.
    /// </summary>
    [HarmonyPatch]
    internal static class NarrationAnnouncer
    {
        /// <summary>Private static list of the quote types queued for display.</summary>
        private static readonly FieldInfo QuoteTypeField =
            AccessTools.Field(typeof(QuoteScreenController), "QuoteType");

        /// <summary>
        /// Announce a quote card as soon as it is requested.
        ///
        /// The text is derived from the same localisation key the game is about to use
        /// rather than read back off the TMP component. In the fading branch the game
        /// assigns <c>Text.text</c> inside a two-second tween callback, so reading the
        /// component here would return the <em>previous</em> quote — and reading it later
        /// would mean racing a tween.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(QuoteScreenController), "SetQuote")]
        private static void AnnounceQuote()
        {
            if (!Plugin.SpeechEnabled.Value) return;

            var quote = CurrentQuoteText();
            if (Plugin.LogDialogue.Value)
                Plugin.Log.LogInfo($"[narration quote] index={QuoteScreenController.CURRENT_QUOTE_INDEX} text=\"{quote}\"");

            if (string.IsNullOrEmpty(quote)) return;

            Speaker.Say(quote);
        }

        /// <summary>Cutscene subtitles, shown over letterboxed scenes.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LetterBox), nameof(LetterBox.ShowSubtitle))]
        private static void AnnounceSubtitle(string text)
        {
            if (!Plugin.SpeechEnabled.Value) return;

            var clean = RichText.Clean(text);
            if (Plugin.LogDialogue.Value)
                Plugin.Log.LogInfo($"[narration subtitle] text=\"{clean}\"");

            if (clean.Length == 0) return;

            Speaker.Say(clean);
        }

        private static string CurrentQuoteText()
        {
            if (QuoteTypeField == null) return null;

            if (!(QuoteTypeField.GetValue(null) is IList queued) || queued.Count == 0) return null;

            var index = QuoteScreenController.CURRENT_QUOTE_INDEX;
            if (index < 0 || index >= queued.Count) return null;

            var term = $"QUOTE/{queued[index]}";
            var translated = LocalizationManager.GetTranslation(term);
            return RichText.IsUsableLocalization(translated, term)
                ? RichText.Clean(translated)
                : null;
        }
    }
}
