using System;
using System.Collections.Generic;
using CultAccess.Util;

namespace CultAccess.Speech
{
    /// <summary>
    /// Named SpeechPriority rather than Priority: HarmonyLib exports its own Priority enum
    /// for patch ordering, and every file that uses both needs an alias otherwise.
    /// </summary>
    public enum SpeechPriority
    {
        /// <summary>Queue behind whatever is speaking. For ambient/background info.</summary>
        Queued,

        /// <summary>
        /// Replaces its own kind and nothing else. For a running commentary that repeats:
        /// walking directions, where the newest instruction makes the previous one obsolete
        /// but neither is worth destroying a world event over.
        ///
        /// Navigation used <see cref="Now"/> and it was wrong in both directions. Turning
        /// quickly produced instructions faster than they could be spoken, and every one of
        /// them cancelled all pending speech — so a follower dying or a building finishing
        /// could be wiped by a direction the player was about to hear a corrected version of
        /// anyway. Losing an instruction costs nothing, because another follows in under a
        /// second; losing a world event costs the event.
        /// </summary>
        Superseding,

        /// <summary>Cut off current speech. Correct for focus changes and user-driven reads.</summary>
        Now,
    }

    /// <summary>
    /// The single funnel for everything the mod says.
    ///
    /// Deliberately does not implement its own utterance queue: screen readers already
    /// queue properly and know the user's speech rate, so we pass <c>interrupt=false</c>
    /// and let NVDA/JAWS sequence it. Our job is provider selection, markup cleanup,
    /// duplicate suppression, and history for the repeat hotkey.
    /// </summary>
    public static class Speaker
    {
        private static ISpeechProvider _provider;
        private static readonly List<string> History = new List<string>();
        private const int MaxHistory = 50;

        private static string _lastText = string.Empty;
        private static float _lastTime = float.NegativeInfinity;

        /// <summary>Whether the most recent utterance was superseding, and so may be cut off.</summary>
        private static bool _lastWasSuperseding;

        /// <summary>Repeated identical text inside this window is dropped.</summary>
        public static float DuplicateWindowSeconds = 0.35f;

        /// <summary>
        /// Mirror every announcement to a refreshable braille display. On by default:
        /// some users read braille rather than listening, and for them this is the only
        /// usable output channel.
        /// </summary>
        public static bool BrailleEnabled = true;

        public static bool Ready => _provider != null;
        public static string ProviderName => _provider?.Name ?? "none";
        public static bool BrailleAvailable => _provider != null && _provider.SupportsBraille;

        /// <summary>
        /// Probe providers in order of quality and keep the first that works.
        /// <paramref name="nativeDir"/> is where Tolk.dll / nvdaControllerClient64.dll live.
        /// </summary>
        public static void Initialize(string nativeDir)
        {
            var candidates = new ISpeechProvider[]
            {
                new TolkProvider(),
                new NvdaProvider(),
                new SapiProvider(),
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    if (candidate.TryInitialize(nativeDir))
                    {
                        _provider = candidate;
                        Plugin.Log.LogInfo($"Speech provider: {candidate.Name}");
                        return;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Provider {candidate.Name} threw during init: {e}");
                }

                candidate.Dispose();
            }

            Plugin.Log.LogError("No speech provider available — the mod cannot announce anything.");
        }

        /// <summary>Speak <paramref name="text"/> after cleaning markup. Safe to call with junk.</summary>
        public static void Say(string text, SpeechPriority priority = SpeechPriority.Now)
        {
            if (_provider == null) return;

            // Measured because the largest stalls in the update loop landed outside every
            // subsystem scope, and speaking is the one thing those regions have in common.
            // A screen reader is reached by a blocking call into a native library; if that
            // library is slow to answer, the game's frame waits on it. This scope is what
            // says whether that is happening rather than leaving it a plausible story.
            var timing = Diagnostics.FrameBudget.Begin();
            try
            {
                SayInternal(text, priority);
            }
            finally
            {
                Diagnostics.FrameBudget.End("speech", timing);
            }
        }

        private static void SayInternal(string text, SpeechPriority priority)
        {

            var clean = RichText.Clean(text);
            if (clean.Length == 0) return;

            var now = UnityEngine.Time.unscaledTime;
            if (clean == _lastText && now - _lastTime < DuplicateWindowSeconds)
            {
                LogSpoken("suppressed-duplicate", clean, priority);
                return;
            }

            LogSpoken("spoken", clean, priority);

            // Superseding interrupts only when the thing it would cut off is another of its
            // own kind. The provider's cancel is all-or-nothing, so the alternative — cancel
            // always — would let a direction destroy a queued announcement, and never
            // cancelling would let rapid turns pile a backlog the player cannot skip past.
            var interrupt = priority == SpeechPriority.Now ||
                            (priority == SpeechPriority.Superseding && _lastWasSuperseding);
            _lastWasSuperseding = priority == SpeechPriority.Superseding;

            _lastText = clean;
            _lastTime = now;

            History.Add(clean);
            if (History.Count > MaxHistory) History.RemoveAt(0);

            try
            {
                _provider.Speak(clean, interrupt);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Speak failed: {e.Message}");
            }

            if (!BrailleEnabled || !_provider.SupportsBraille) return;

            try
            {
                _provider.Braille(clean);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Braille failed: {e.Message}");
            }
        }

        /// <summary>
        /// Record what was actually said. This is the one thing a session log could never
        /// answer before: every announcer that logs does so from its own side, in its own
        /// wording, and most do not log at all — so "what did I hear?" and "why did I not
        /// hear that?" had no evidence behind them.
        ///
        /// The text logged here is the final string handed to the screen reader, after
        /// cleaning, which is exactly what the player heard. A question about icon debris
        /// reaching the reader was answerable only by reasoning about which paths clean and
        /// which do not; with this it is a search.
        ///
        /// Suppressed duplicates are logged too, and deliberately. A line that never arrived
        /// because it repeated one from 0.3 seconds ago looks identical, from the player's
        /// side, to a line that was never generated.
        /// </summary>
        private static void LogSpoken(string outcome, string clean, SpeechPriority priority)
        {
            if (!Plugin.LogSpeech.Value) return;

            Plugin.Log.LogInfo(
                $"[speech] {outcome} priority={priority} text=\"{clean.Replace("\"", "'")}\"");
        }

        /// <summary>Join non-empty parts with ", " and speak them as one utterance.</summary>
        public static void SayParts(SpeechPriority priority, params string[] parts)
        {
            if (parts == null || parts.Length == 0) return;

            var joined = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                var clean = RichText.Clean(part);
                if (clean.Length == 0) continue;
                if (joined.Length > 0) joined.Append(", ");
                joined.Append(clean);
            }

            Say(joined.ToString(), priority);
        }

        public static void Silence()
        {
            if (_provider == null) return;
            try { _provider.Silence(); } catch { /* best effort */ }
        }

        /// <summary>
        /// Re-speak recent output. <paramref name="stepsBack"/> 0 is the most recent line.
        /// Bypasses duplicate suppression, since repeating is the whole point.
        /// </summary>
        public static void RepeatHistory(int stepsBack = 0)
        {
            if (_provider == null || History.Count == 0) return;

            var index = History.Count - 1 - Math.Max(0, stepsBack);
            if (index < 0) index = 0;

            _lastText = string.Empty;
            _provider.Speak(History[index], true);

            if (BrailleEnabled && _provider.SupportsBraille)
                _provider.Braille(History[index]);
        }

        public static int HistoryCount => History.Count;

        public static void Shutdown()
        {
            _provider?.Dispose();
            _provider = null;
        }
    }
}
