using System;
using CultAccess.Speech;
using FMODUnity;

namespace CultAccess.Diagnostics
{
    /// <summary>
    /// Records which device the game's audio is going to, and how much delay FMOD itself
    /// adds. Once per run, and log-only with one deliberate exception: a non-stereo output is
    /// also spoken, because it degrades the mod's own positioning and can silence the game's
    /// spatialised sounds, and a log line cannot reach the player it affects.
    ///
    /// Written because a report of lag during weapon attacks turned out to be worth splitting
    /// in two. The frame-budget numbers measure wall-clock time inside the mod's own code, so
    /// they can only ever see *compute* stalls. They cannot see the other half of the chain:
    /// how long a sound takes to reach the player's ears after the game has finished with it.
    ///
    /// **That half can be much the larger one.** A wireless headset adds output delay
    /// downstream of the game entirely — in the operating system, the driver and the headset —
    /// and the game has no way to observe it. For a player whose only feedback channel is
    /// audio, that delay is subjectively indistinguishable from the game responding slowly,
    /// which is precisely how it gets reported.
    ///
    /// What is written here is what can honestly be known: the device name, the mixer format,
    /// and FMOD's own buffer latency. **The wireless link's own latency is not included and
    /// cannot be measured from here.** The value of the line is that it names the output path,
    /// so a future timing report can be read in the light of what it was played through.
    /// </summary>
    internal static class AudioOutputInfo
    {
        private const int NameLength = 256;

        internal static bool Enabled = true;

        /// <summary>
        /// Whether a non-stereo output is announced as well as logged. On by default: the
        /// player it matters most to is the one who cannot see the sound settings.
        /// </summary>
        internal static bool WarnOnNonStereo = true;

        private static bool _done;

        internal static void RunOnce()
        {
            if (_done || !Enabled) return;
            _done = true;

            try
            {
                var system = RuntimeManager.CoreSystem;

                if (system.getDriver(out var driver) != FMOD.RESULT.OK) return;

                if (system.getDriverInfo(
                        driver, out var name, NameLength, out _, out var rate,
                        out var speakerMode, out var channels) == FMOD.RESULT.OK)
                {
                    Plugin.Log.LogInfo(
                        $"[audio out] device=\"{name}\" rate={rate} mode={speakerMode} " +
                        $"channels={channels}");

                    ReportSpeakerMode(speakerMode, channels);
                }

                if (system.getDSPBufferSize(out var bufferLength, out var buffers) !=
                    FMOD.RESULT.OK)
                    return;

                if (system.getSoftwareFormat(out var mixRate, out _, out _) != FMOD.RESULT.OK)
                    return;

                // FMOD's own contribution only. Everything after it — the OS mixer, the
                // driver, a wireless link — is invisible from here and is usually larger.
                var mixerLatencyMs = mixRate > 0
                    ? bufferLength * buffers * 1000.0 / mixRate
                    : 0.0;

                Plugin.Log.LogInfo(
                    $"[audio out] mixerBuffer={bufferLength}x{buffers} " +
                    $"mixerLatency={mixerLatencyMs:0.0}ms " +
                    "(this is FMOD's share only; any wireless headset adds its own delay " +
                    "downstream, which nothing here can see)");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[audio out] could not read the output device: {e.Message}");
            }
        }

        /// <summary>
        /// Say something when the output is not stereo, because nothing else will.
        ///
        /// The whole cue vocabulary is stereo pan — left and right is position, and that is
        /// what the help text promises. On a surround mix the pan is spread across channels a
        /// headset does not reproduce, so our own positioning degrades, and the game's own
        /// spatialised sounds can disappear entirely while music and menus keep playing.
        /// That combination was reported in play as "the game's sounds are broken".
        ///
        /// A sighted player would eventually go and look at the sound settings. A player whose
        /// only channel is audio has no way to notice at all, and the state can be wrong
        /// without them having changed anything: Windows kept an 8-channel format on this
        /// machine after spatial sound had been switched back off.
        ///
        /// Spoken as well as logged, and this is the exception to diagnostics being log-only.
        /// A line in a log cannot reach someone whose game has just gone quiet.
        /// </summary>
        private static void ReportSpeakerMode(FMOD.SPEAKERMODE speakerMode, int channels)
        {
            if (speakerMode == FMOD.SPEAKERMODE.STEREO) return;

            Plugin.Log.LogWarning(
                $"[audio out] output is {speakerMode} with {channels} channels, not stereo. " +
                "Cue direction is stereo pan, so positioning degrades on a surround mix, and " +
                "the game's own spatialised sounds may be routed to speakers that do not " +
                "exist. Windows can report this after spatial sound has been turned off again.");

            if (!WarnOnNonStereo) return;

            Speaker.Say(
                $"Warning. Audio output is {Describe(speakerMode)}, not stereo. " +
                "Sound direction may be unreliable and some game sounds may be missing. " +
                "Check spatial sound and the speaker configuration for this device in Windows " +
                "sound settings.",
                SpeechPriority.Queued);
        }

        /// <summary>FMOD's enum names read badly aloud: _7POINT1 is not a phrase.</summary>
        private static string Describe(FMOD.SPEAKERMODE speakerMode)
        {
            switch (speakerMode)
            {
                case FMOD.SPEAKERMODE.MONO: return "mono";
                case FMOD.SPEAKERMODE.QUAD: return "quadraphonic";
                case FMOD.SPEAKERMODE.SURROUND: return "surround";
                case FMOD.SPEAKERMODE.RAW: return "a raw multichannel format";
                case FMOD.SPEAKERMODE._5POINT1: return "5.1 surround";
                case FMOD.SPEAKERMODE._7POINT1: return "7.1 surround";
                case FMOD.SPEAKERMODE._7POINT1POINT4: return "7.1.4 surround";
                default: return speakerMode.ToString();
            }
        }
    }
}
