using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CultAccess.Navigation;
using FMODUnity;
using UnityEngine;

namespace CultAccess.Audio
{
    /// <summary>
    /// Plays one generated cue, positioned in the stereo field.
    ///
    /// The single funnel for every non-speech sound except the navigation beacon, which keeps
    /// its own tested path. Pan carries screen left and right, pitch carries screen up and
    /// down, matching the beacon's already-learned directional language.
    ///
    /// Raw FMOD rather than Unity audio: this build ships with Unity audio disabled, and an
    /// AudioSource here produced <c>clipSamples=0</c> and complete silence with no error.
    /// FMOD is demonstrably alive, because it is what the game itself plays through.
    /// </summary>
    internal static class CuePlayer
    {
        // Combat cues are much shorter than the beacon ping, so they get a wider stereo
        // image: a modest screen offset must still be obvious under weapon audio.
        private const float HorizontalScreenFraction = 0.24f;
        private const float VerticalScreenFraction = 0.35f;
        private const float LowestDirectionalPitch = 0.75f;
        private const float HighestDirectionalPitch = 1.45f;

        // Ambient categories are identified by timbre, and timbre is mostly pitch. Their
        // vertical channel is therefore compressed hard: enough to hear above from below,
        // not enough to turn one category into another.
        private const float LowestAmbientPitch = 0.88f;
        private const float HighestAmbientPitch = 1.14f;

        private static readonly HashSet<CueId> Failed = new HashSet<CueId>();
        private static readonly HashSet<CueId> Logged = new HashSet<CueId>();
        private static bool _loggedMissingCamera;

        /// <summary>
        /// When the last life-threatening cue played, so lower-priority cues can stand aside
        /// rather than mask it.
        /// </summary>
        internal static float LastCriticalAt { get; private set; } = float.NegativeInfinity;

        internal static bool CriticalRecently(float seconds) =>
            Time.unscaledTime - LastCriticalAt < seconds;

        /// <summary>Play a cue positioned at a world point. False when nothing sounded.</summary>
        internal static bool Play(CueId cue, Vector3 worldPosition, float volumeScale = 1f)
        {
            if (!CueSettings.Enabled(cue)) return false;
            if (!TrySpatialize(cue, worldPosition, out var pan, out var pitch)) return false;
            return PlayDirect(cue, pan, pitch, volumeScale);
        }

        /// <summary>
        /// Play a cue at an explicit stereo position. Used by the learn-sounds walkthrough,
        /// which must demonstrate a cue with no world object to point at, and by the wall
        /// sonar, whose directions are rays rather than objects.
        /// </summary>
        internal static bool PlayDirect(
            CueId cue,
            float pan,
            float pitch,
            float volumeScale = 1f,
            bool ignoreEnabled = false)
        {
            if (!ignoreEnabled && !CueSettings.Enabled(cue)) return false;
            if (Failed.Contains(cue) || !TryGetSound(cue, out var sound)) return false;

            var volume = CueSettings.ChannelVolume(cue, volumeScale);
            if (volume <= 0f) return false;

            try
            {
                var result = RuntimeManager.CoreSystem.playSound(
                    sound, default(FMOD.ChannelGroup), false, out var channel);
                if (result != FMOD.RESULT.OK)
                {
                    Plugin.Log.LogWarning($"Cue {cue} playSound failed: {result}");
                    return false;
                }

                channel.setVolume(volume);
                channel.setPan(Mathf.Clamp(pan, -1f, 1f));
                channel.setPitch(Mathf.Max(0.05f, pitch));

                if (IsCritical(cue)) LastCriticalAt = Time.unscaledTime;

                if (Logged.Add(cue))
                    Plugin.Log.LogInfo(
                        $"[cue audio] first={cue} pan={pan:0.00} pitch={pitch:0.00} " +
                        $"volume={volume:0.00}");

                return true;
            }
            catch (Exception e)
            {
                Failed.Add(cue);
                Plugin.Log.LogError($"Cue {cue} playback failed; disabling it: {e}");
                return false;
            }
        }

        /// <summary>
        /// Project a world point into the mod's directional language. False when there is no
        /// player or camera, in which case a directional cue would be a lie.
        /// </summary>
        internal static bool TrySpatialize(CueId cue, Vector3 target, out float pan, out float pitch)
        {
            pan = 0f;
            pitch = 1f;

            var listener = NavigatorPlayer.Resolve();
            var camera = Camera.main;
            if (listener == null || camera == null)
            {
                if (!_loggedMissingCamera)
                {
                    _loggedMissingCamera = true;
                    Plugin.Log.LogWarning(
                        "[cue audio] no active player/camera; directional cue suppressed");
                }
                return false;
            }

            var from = camera.WorldToScreenPoint(listener.position);
            var to = camera.WorldToScreenPoint(target);
            var horizontalScale = Mathf.Max(1f, Screen.width * HorizontalScreenFraction);
            var verticalScale = Mathf.Max(1f, Screen.height * VerticalScreenFraction);

            pan = Mathf.Clamp((to.x - from.x) / horizontalScale, -1f, 1f);
            var vertical = Mathf.Clamp((to.y - from.y) / verticalScale, -1f, 1f);
            pitch = VerticalToPitch(cue, vertical);
            return true;
        }

        /// <summary>Map a normalised vertical screen offset to this cue's pitch range.</summary>
        internal static float VerticalToPitch(CueId cue, float vertical)
        {
            var low = Cues.IsAmbient(cue) ? LowestAmbientPitch : LowestDirectionalPitch;
            var high = Cues.IsAmbient(cue) ? HighestAmbientPitch : HighestDirectionalPitch;
            return Mathf.Lerp(low, high, (Mathf.Clamp(vertical, -1f, 1f) + 1f) * 0.5f);
        }

        internal static void Shutdown()
        {
            SoundBank.Shutdown();
            Failed.Clear();
            Logged.Clear();
            LastCriticalAt = float.NegativeInfinity;
        }

        /// <summary>
        /// Cues that mean the player is about to take damage. They set the suppression
        /// window that keeps ambient and wall cues from masking them.
        /// </summary>
        private static bool IsCritical(CueId cue) =>
            cue == CueId.MeleeThreat ||
            cue == CueId.ProjectileThreat ||
            cue == CueId.AreaThreat ||
            cue == CueId.StaticTrap;

        /// <summary>
        /// Resolve the cue's sound, preferring a file the player dropped in <c>sounds/</c>
        /// over the generated waveform. See <see cref="SoundBank"/>.
        /// </summary>
        private static bool TryGetSound(CueId cue, out FMOD.Sound sound)
        {
            if (SoundBank.TryGet(
                    Cues.SoundFileName(cue), false, () => CueLibrary.Generate(cue), out sound))
                return true;

            Failed.Add(cue);
            return false;
        }
    }
}
