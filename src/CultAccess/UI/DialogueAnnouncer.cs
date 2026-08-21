using System.Collections.Generic;
using System.Reflection;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using I2.Loc;
using MMTools;
using TMPro;
using UnityEngine;

namespace CultAccess.UI
{
    /// <summary>
    /// Announces conversation dialogue: story scenes, NPC talk, and barks.
    ///
    /// Text is read from the live UI components rather than resolved from localisation
    /// terms. A TMP typewriter assigns the whole string up front and animates
    /// <c>maxVisibleCharacters</c>, so the component's <c>text</c> already holds the
    /// complete line — and it is correct regardless of whether the line came from a term,
    /// a runtime-built string, or a speaker name override such as
    /// <c>MMConversation.SetTitle</c>. Term lookup remains as a fallback.
    ///
    /// Both <c>ShowNextLine</c> and <c>UpdateText</c> are hooked. <c>UpdateText</c> alone
    /// is small enough that Mono may inline it into its caller, which would stop a patch on
    /// it from ever firing; <c>ShowNextLine</c> is large enough to be safe. Duplicate
    /// suppression in <see cref="Speaker"/> means the overlap costs nothing.
    ///
    /// Response options need no work here: <c>DialogueWheel.Update</c> selects options via
    /// <c>UINavigatorNew.NavigateToNew</c>, so <see cref="FocusAnnouncer"/> already reads
    /// the highlighted choice.
    /// </summary>
    [HarmonyPatch]
    internal static class DialogueAnnouncer
    {
        /// <summary>
        /// Reached by reflection: the field's type lives in the Text Animator package, which
        /// we deliberately do not reference. We only need the GameObject it sits on, because
        /// the TMP component holding the dialogue text is on that same object.
        /// </summary>
        private static readonly FieldInfo TextPlayerField =
            AccessTools.Field(typeof(MMConversation), "TextPlayer");


        [HarmonyPostfix]
        [HarmonyPatch(typeof(MMConversation), "ShowNextLine")]
        private static void AfterShowNextLine() => AnnounceLine("ShowNextLine");

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MMConversation), "UpdateText")]
        private static void AfterUpdateText() => AnnounceLine("UpdateText");

        private static void AnnounceLine(string source)
        {
            if (!Plugin.SpeechEnabled.Value) return;

            var isBark = MMConversation.isBark;
            if (isBark && !Plugin.AnnounceBarks.Value) return;

            // ShowNextLine is invoked once more after the last entry while the old TMP
            // text is still on screen. It is transition cleanup, not another dialogue
            // line; accepting it would repeat the final sentence several seconds later.
            var conversation = MMConversation.CURRENT_CONVERSATION;
            var position = MMConversation.Position;
            if (conversation?.Entries != null &&
                (position < 0 || position >= conversation.Entries.Count))
            {
                if (Plugin.LogDialogue.Value)
                {
                    Plugin.Log.LogInfo(
                        $"[dialogue via {source}] skipped terminal stale text " +
                        $"position={position} entries={conversation.Entries.Count} bark={isBark}");
                }
                return;
            }

            var body = LiveBodyText() ?? TermBodyText();
            // The title TMP can retain the previous speaker until UpdateText runs. The
            // current entry is authoritative when it names somebody; live text remains
            // the fallback for runtime SetTitle overrides and entries with no name term.
            var entry = CurrentEntry();
            var speaker = TermSpeakerName();
            if (speaker == null && (entry == null || source == "UpdateText"))
                speaker = LiveSpeakerName();

            // A speaker that still contains angle brackets is markup debris, not a name.
            // The "color>" case this caught is fixed above; the guard stays as a backstop
            // for shapes not yet seen, because speaking debris is worse than speaking
            // nothing, and the raw string is what will identify the next one.
            if (RichText.HasMarkupResidue(speaker))
            {
                var rawTitle = MMConversation.mmConversation?.TitleText?.text;
                Plugin.Log.LogWarning(
                    $"[dialogue speaker residue] cleaned={Quote(speaker)} " +
                    $"rawTitle={Quote(rawTitle)} source={source}");
                speaker = null;
            }

            if (Plugin.LogDialogue.Value)
            {
                Plugin.Log.LogInfo(
                    $"[dialogue via {source}] position={MMConversation.Position} bark={isBark} " +
                    $"speaker={Quote(speaker)} body={Quote(body)}");
            }

            if (string.IsNullOrEmpty(body)) return;

            // Barks are ambient chatter from passing followers; letting them cut off a
            // story line the player is listening to would be worse than dropping them.
            var priority = isBark ? SpeechPriority.Queued : SpeechPriority.Now;

            if (string.IsNullOrEmpty(speaker))
                Speaker.Say(body, priority);
            else
                Speaker.SayParts(priority, speaker, body);
        }

        /// <summary>
        /// Tell the player a choice is pending and what the options are.
        ///
        /// Hooked on the wheel's own <c>OnEnable</c>, which is the exact moment the choices
        /// become available. The earlier hook on <c>MMConversation.OnPrintCompleted</c> was
        /// wrong twice over: the game only shows the wheel once <c>Position</c> reaches the
        /// last entry, a condition I did not replicate, so choices were announced at the
        /// start of a conversation instead of at the end of it; and because
        /// <c>MMConversation.Play</c> adds that listener without ever removing it, listeners
        /// accumulate and it fires repeatedly.
        ///
        /// Without this cue there is no indication a choice exists at all, since the wheel
        /// selects nothing until the stick is pushed.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DialogueWheel), "OnEnable")]
        private static void AnnounceChoices()
        {
            if (!Plugin.SpeechEnabled.Value) return;

            var conversation = MMConversation.CURRENT_CONVERSATION;
            var responses = conversation?.Responses;
            if (responses == null || responses.Count == 0) return;

            var labels = new List<string>(responses.Count);
            foreach (var response in responses)
            {
                if (response == null) continue;

                var label = Translate(response.Term) ?? response.text;
                label = RichText.Clean(label);
                if (label.Length > 0) labels.Add(label);
            }

            if (labels.Count == 0) return;

            var count = labels.Count == 1 ? "1 choice" : $"{labels.Count} choices";
            Speaker.Say($"{count}: {string.Join(", ", labels.ToArray())}", SpeechPriority.Queued);
        }

        /// <summary>The dialogue line as currently assigned to the TMP component.</summary>
        private static string LiveBodyText()
        {
            var conversation = MMConversation.mmConversation;
            if (conversation == null || TextPlayerField == null) return null;

            if (!(TextPlayerField.GetValue(conversation) is Component player)) return null;

            var tmp = player.GetComponent<TMP_Text>();
            if (tmp == null) return null;

            var clean = RichText.Clean(tmp.text);
            return clean.Length == 0 ? null : clean;
        }

        /// <summary>The speaker name as currently rendered, tags stripped.</summary>
        private static string LiveSpeakerName()
        {
            var conversation = MMConversation.mmConversation;
            if (conversation == null || conversation.TitleText == null) return null;

            var clean = RichText.Clean(conversation.TitleText.text);
            return clean.Length == 0 ? null : clean;
        }

        private static string TermBodyText()
        {
            var entry = CurrentEntry();
            if (entry == null) return null;

            var line = Translate(entry.TermToSpeak) ?? entry.DisplayText;
            line = RichText.Clean(line);
            return line.Length == 0 ? null : line;
        }

        /// <summary>
        /// Mirrors the game's own convention: "-" and empty mean unset, and a term with no
        /// translation falls back to the raw term rather than to nothing.
        ///
        /// A name that is not a term is reduced by <see cref="SpeakerNameText"/>, which
        /// documents why markup has to come off before the path is split.
        /// </summary>
        private static string TermSpeakerName()
        {
            var entry = CurrentEntry();
            if (entry == null) return null;

            var name = entry.CharacterName;
            if (string.IsNullOrEmpty(name) || name == "-") return null;

            // Term paths carry no markup, so looking the raw value up first costs nothing
            // and keeps genuine terms exact.
            var translated = Translate(name);
            if (translated != null) return translated;

            return SpeakerNameText.FromRawCharacterName(name);
        }

        private static ConversationEntry CurrentEntry()
        {
            var conversation = MMConversation.CURRENT_CONVERSATION;
            if (conversation?.Entries == null) return null;

            var position = MMConversation.Position;
            if (position < 0 || position >= conversation.Entries.Count) return null;

            return conversation.Entries[position];
        }

        /// <summary>
        /// Returns cleaned text, never the raw translation. The catalogue colours and inlines
        /// sprites into names as freely as it does into body lines, and a speaker name went
        /// straight to the reader without a cleaning pass before this.
        /// </summary>
        private static string Translate(string term)
        {
            if (string.IsNullOrEmpty(term) || term == "-") return null;

            var translated = LocalizationManager.GetTranslation(term);
            return RichText.IsUsableLocalization(translated, term)
                ? RichText.Clean(translated)
                : null;
        }

        private static string Quote(string value) => value == null ? "<null>" : $"\"{value}\"";
    }
}
