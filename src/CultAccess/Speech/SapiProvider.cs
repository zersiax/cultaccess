using System;
using System.Reflection;

namespace CultAccess.Speech
{
    /// <summary>
    /// Last-resort provider: Windows SAPI driven over COM through late binding.
    /// System.Speech is not part of Unity's Mono profile, so we go through the
    /// SAPI.SpVoice ProgID instead. Needs no native files at all, which makes it
    /// the guaranteed-working baseline — but it speaks over the user's screen
    /// reader rather than through it, so it is only used when nothing else loads.
    /// </summary>
    internal sealed class SapiProvider : ISpeechProvider
    {
        // SpeechVoiceSpeakFlags
        private const int SVSFlagsAsync = 1;
        private const int SVSFPurgeBeforeSpeak = 2;

        private object _voice;
        private Type _voiceType;

        public string Name => "Windows SAPI (fallback)";

        public bool TryInitialize(string nativeDir)
        {
            try
            {
                _voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (_voiceType == null) return false;

                _voice = Activator.CreateInstance(_voiceType);
                if (_voice == null) return false;

                // Prove the pipe works before claiming this provider.
                Invoke("Speak", string.Empty, SVSFlagsAsync);
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"SAPI initialisation failed: {e.Message}");
                _voice = null;
                return false;
            }
        }

        public void Speak(string text, bool interrupt)
        {
            if (_voice == null) return;

            var flags = SVSFlagsAsync | (interrupt ? SVSFPurgeBeforeSpeak : 0);
            try
            {
                Invoke("Speak", text, flags);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"SAPI Speak failed: {e.Message}");
            }
        }

        /// <summary>SAPI is speech synthesis only; it has no braille channel.</summary>
        public bool SupportsBraille => false;

        public void Braille(string text)
        {
        }

        public void Silence()
        {
            // Purging with an empty utterance is the documented way to stop SAPI.
            if (_voice == null) return;
            try { Invoke("Speak", string.Empty, SVSFlagsAsync | SVSFPurgeBeforeSpeak); }
            catch { /* nothing useful to do */ }
        }

        private void Invoke(string member, params object[] args) =>
            _voiceType.InvokeMember(member, BindingFlags.InvokeMethod, null, _voice, args);

        public void Dispose()
        {
            _voice = null;
            _voiceType = null;
        }
    }
}
