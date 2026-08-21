using System;
using FMODUnity;

namespace CultAccess.Audio
{
    /// <summary>
    /// A sustained tone held for exactly as long as a timing window is open.
    ///
    /// The cooking minigame evaluates nothing until the player presses Interact: the tracker
    /// ping-pongs indefinitely and only its position at that instant matters. A single chirp
    /// therefore conveys an onset but not a duration, which leaves the player guessing how
    /// long they have — and on faster recipes the guess is the whole difficulty.
    ///
    /// A tone that starts when the window opens and stops when it closes carries all three
    /// facts a sighted player reads off the bar: it is open, it is still open, it has closed.
    /// Press any time you can hear it.
    ///
    /// Raw FMOD, like the beacon and the chirp, because this build disables Unity audio.
    /// </summary>
    internal static class TimingTone
    {
        private const float Frequency = 1180f;

        /// <summary>
        /// One full cycle count chosen so the loop point falls exactly on a zero crossing.
        /// A partial period would click audibly on every wrap, several times a second.
        /// </summary>
        private const int Cycles = 8;

        private static FMOD.Sound _sound;
        private static FMOD.Channel _channel;
        private static bool _ready;
        private static bool _failed;
        private static bool _playing;

        /// <summary>
        /// Read from the shared cue settings, so the sustained window tone is switchable and
        /// adjustable in the same place as every other sound rather than only in the file.
        /// </summary>
        internal static float Volume => CueSettings.Volume(CueId.TimingWindow);

        /// <summary>
        /// Begin the tone. <paramref name="ignoreEnabled"/> is for the learn-sounds preview,
        /// which must demonstrate the cue even while it is switched off — a preview that
        /// stayed silent because the thing was off is indistinguishable from a broken sound,
        /// and hearing it first is how a player decides whether to switch it on.
        /// </summary>
        internal static void Start(bool ignoreEnabled = false)
        {
            if (!ignoreEnabled && !CueSettings.Enabled(CueId.TimingWindow)) return;
            if (_playing || _failed || !EnsureSound()) return;

            try
            {
                var result = RuntimeManager.CoreSystem.playSound(
                    _sound, default(FMOD.ChannelGroup), false, out _channel);
                if (result != FMOD.RESULT.OK)
                {
                    Plugin.Log.LogWarning($"Timing tone playSound failed: {result}");
                    return;
                }

                _channel.setVolume(Volume);
                _channel.setPan(0f);
                _playing = true;
            }
            catch (Exception e)
            {
                _failed = true;
                Plugin.Log.LogError($"Timing tone playback threw; disabling tone: {e}");
            }
        }

        internal static void Stop()
        {
            if (!_playing) return;
            _playing = false;

            try
            {
                _channel.stop();
            }
            catch (Exception e)
            {
                // A channel the system already reclaimed is not an error worth escalating,
                // but silence here would hide a real audio fault.
                Plugin.Log.LogWarning($"Timing tone stop failed: {e.Message}");
            }
        }

        internal static void Shutdown()
        {
            Stop();

            // The sound belongs to SoundBank, which releases every mod sound together.
            _sound = default;
            _ready = false;
        }

        /// <summary>
        /// Resolve the sustained tone, preferring a <c>timing-window</c> file the player
        /// supplied in <c>sounds/</c>. Requested as looping, because its whole purpose is to
        /// last exactly as long as the window is open.
        /// </summary>
        private static bool EnsureSound()
        {
            if (_ready) return true;
            if (_failed) return false;

            if (!SoundBank.TryGet(
                    Cues.SoundFileName(CueId.TimingWindow), true,
                    () => Waveform.Loop(Frequency, Cycles, 1f), out _sound))
            {
                _failed = true;
                Plugin.Log.LogError("Timing tone could not be created; tone disabled.");
                return false;
            }

            _ready = true;
            return true;
        }
    }
}
