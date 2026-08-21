using System;
using FMODUnity;

namespace CultAccess.Audio
{
    /// <summary>
    /// A short rising chirp used to expose a visual timing window. It follows the same
    /// raw-FMOD path as the tested navigation beacon because this game disables Unity
    /// audio and routes its sound through FMOD.
    /// </summary>
    internal static class TimingCue
    {
        private static FMOD.Sound _sound;
        private static bool _ready;
        private static bool _failed;

        internal static void Play()
        {
            if (!CueSettings.Enabled(CueId.TimingChirp)) return;
            if (_failed || !EnsureSound()) return;

            try
            {
                var result = RuntimeManager.CoreSystem.playSound(
                    _sound, default(FMOD.ChannelGroup), false, out var channel);
                if (result != FMOD.RESULT.OK)
                {
                    Plugin.Log.LogWarning($"Timing cue playSound failed: {result}");
                    return;
                }

                channel.setVolume(CueSettings.ChannelVolume(CueId.TimingChirp, 1f));
                channel.setPan(0f);
            }
            catch (Exception e)
            {
                _failed = true;
                Plugin.Log.LogError($"Timing cue playback threw; disabling cue: {e}");
            }
        }

        /// <summary>
        /// Resolve the chirp, preferring a <c>timing-edge</c> file the player supplied in
        /// <c>sounds/</c>. See <see cref="SoundBank"/>.
        /// </summary>
        private static bool EnsureSound()
        {
            if (_ready) return true;
            if (_failed) return false;

            if (!SoundBank.TryGet(
                    Cues.SoundFileName(CueId.TimingChirp), false,
                    () => CueLibrary.Generate(CueId.TimingChirp), out _sound))
            {
                _failed = true;
                Plugin.Log.LogError("Timing cue could not be created; cue disabled.");
                return false;
            }

            _ready = true;
            return true;
        }
    }
}
