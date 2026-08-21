using System;

namespace CultAccess.Speech
{
    /// <summary>
    /// A single way of getting text out to the user. Providers are probed in priority
    /// order at startup and the first one that initialises wins.
    /// </summary>
    internal interface ISpeechProvider : IDisposable
    {
        /// <summary>Human-readable name for logs and the in-game diagnostics line.</summary>
        string Name { get; }

        /// <summary>Attempt to initialise. Returns false if this provider is unavailable.</summary>
        bool TryInitialize(string nativeDir);

        /// <summary>Speak text. <paramref name="interrupt"/> cancels anything still speaking.</summary>
        void Speak(string text, bool interrupt);

        /// <summary>True when this provider can drive a refreshable braille display.</summary>
        bool SupportsBraille { get; }

        /// <summary>
        /// Send text to the braille display. Separate from <see cref="Speak"/> because
        /// braille is a distinct channel: it has no interrupt concept (each message simply
        /// replaces what is shown) and some users read braille instead of listening.
        /// </summary>
        void Braille(string text);

        /// <summary>Stop speech immediately.</summary>
        void Silence();
    }
}
