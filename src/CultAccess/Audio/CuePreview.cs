using FMODUnity;
using UnityEngine;

namespace CultAccess.Audio
{
    /// <summary>
    /// Plays a cue on demand so the player can learn it away from the moment it matters.
    ///
    /// Previews deliberately ignore the cue's own on/off setting. Someone deciding whether to
    /// switch a cue on needs to hear it first, and a preview that stayed silent because the
    /// thing was off would be indistinguishable from a broken sound.
    ///
    /// The sweep is the part that earns its keep. The single most misunderstood thing about
    /// this mod's audio is what pan and pitch actually encode — pitch is vertical aim, not
    /// distance, and players reasonably assumed otherwise because both change together as
    /// they approach a raised target. Hearing the same cue at left, centre and right, then low
    /// and high, teaches that in a few seconds where a sentence of explanation did not.
    /// </summary>
    internal static class CuePreview
    {
        private const float StepSeconds = 0.42f;

        /// <summary>How long the sustained window tone is demonstrated for.</summary>
        private const float SustainedDemoSeconds = 1.2f;

        /// <summary>How long each wall direction is held during its demonstration.</summary>
        private const float WallDemoStepSeconds = 0.75f;

        private static readonly float[] SweepPans = { -1f, 0f, 1f };
        private static readonly float[] SweepVerticals = { -1f, 0f, 1f };

        /// <summary>
        /// Directions demonstrated for the wall ring, in this order. North and south share a
        /// pan of centre and differ only in note; east and west share a note and differ only
        /// in pan. Hearing those two pairs back to back is the whole mapping in four sounds.
        /// </summary>
        private static readonly int[] WallDemoOrder = { 0, 2, 4, 6 };

        private static CueId _cue;
        private static int _step = -1;
        private static bool _vertical;
        private static float _nextStepAt;
        private static float _stopSustainedAt = float.PositiveInfinity;

        private static int _wallStep = -1;
        private static float _nextWallStepAt;
        private static FMOD.Channel _wallChannel;
        private static bool _wallPlaying;

        /// <summary>True while a multi-step demonstration is still running.</summary>
        internal static bool Running => _step >= 0;

        /// <summary>Play the cue once, centred and level.</summary>
        internal static void Play(CueId cue)
        {
            Cancel();

            if (cue == CueId.AmbientWall)
            {
                // The wall ring is eight sustained tones, not a one-shot, and its direction
                // is carried by note as much as by pan. A single blip would demonstrate
                // neither, so step round the compass instead.
                _wallStep = 0;
                _nextWallStepAt = 0f;
                return;
            }

            if (cue == CueId.TimingWindow)
            {
                // The real thing is a sustained loop, so demonstrate the real thing rather
                // than a fixed-length imitation of it: its whole point is that it lasts.
                TimingTone.Start(ignoreEnabled: true);
                _stopSustainedAt = Time.unscaledTime + SustainedDemoSeconds;
                return;
            }

            CuePlayer.PlayDirect(cue, 0f, CuePlayer.VerticalToPitch(cue, 0f), 1f,
                ignoreEnabled: true);
        }

        /// <summary>
        /// Demonstrate the directional language: the cue at left, centre and right, then at
        /// the bottom, middle and top of its pitch range.
        /// </summary>
        internal static void PlaySweep(CueId cue)
        {
            Cancel();

            if (cue == CueId.TimingWindow || cue == CueId.AmbientWall)
            {
                // The timing tone is centred and undirected by design, and the wall ring
                // already demonstrates its own directions. Neither has anything to sweep.
                Play(cue);
                return;
            }

            _cue = cue;
            _step = 0;
            _vertical = false;
            _nextStepAt = 0f;
        }

        internal static void Cancel()
        {
            _step = -1;
            _wallStep = -1;
            StopWallDemo();

            // Stop the sustained tone here rather than only letting it time out. Cancel runs
            // when the menu closes, and a preview left running with nothing ticking it would
            // hold the tone open for the rest of the session.
            if (!float.IsPositiveInfinity(_stopSustainedAt))
            {
                _stopSustainedAt = float.PositiveInfinity;
                TimingTone.Stop();
            }
        }

        internal static void Tick()
        {
            if (Time.unscaledTime >= _stopSustainedAt)
            {
                _stopSustainedAt = float.PositiveInfinity;
                TimingTone.Stop();
            }

            TickWallDemo();

            if (_step < 0 || Time.unscaledTime < _nextStepAt) return;

            _nextStepAt = Time.unscaledTime + StepSeconds;

            if (!_vertical)
            {
                CuePlayer.PlayDirect(
                    _cue, SweepPans[_step], CuePlayer.VerticalToPitch(_cue, 0f), 1f,
                    ignoreEnabled: true);

                if (++_step < SweepPans.Length) return;

                _step = 0;
                _vertical = true;
                return;
            }

            CuePlayer.PlayDirect(
                _cue, 0f, CuePlayer.VerticalToPitch(_cue, SweepVerticals[_step]), 1f,
                ignoreEnabled: true);

            if (++_step >= SweepVerticals.Length) _step = -1;
        }

        /// <summary>
        /// Step round the compass, holding each direction's tone long enough to hear it as a
        /// tone rather than a blip. Uses the same sounds the live ring uses, including any
        /// replacement the player has supplied, so the lesson matches what they will hear.
        /// </summary>
        private static void TickWallDemo()
        {
            if (_wallStep < 0 || Time.unscaledTime < _nextWallStepAt) return;

            StopWallDemo();

            if (_wallStep >= WallDemoOrder.Length)
            {
                _wallStep = -1;
                return;
            }

            var direction = WallDirection.Ring[WallDemoOrder[_wallStep]];
            _wallStep++;
            _nextWallStepAt = Time.unscaledTime + WallDemoStepSeconds;

            var scheme = WallSonar.Scheme;
            if (!SoundBank.TryGet(
                    direction.SoundKey, true, () => direction.Generate(scheme), out var sound,
                    WallTones.Variant(scheme)))
                return;

            try
            {
                var result = RuntimeManager.CoreSystem.playSound(
                    sound, default(FMOD.ChannelGroup), true, out _wallChannel);
                if (result != FMOD.RESULT.OK) return;

                // Demonstrated at full configured volume rather than the distance-scaled
                // level, so the player hears the loudest it will ever get.
                _wallChannel.setVolume(CueSettings.ChannelVolume(CueId.AmbientWall, 1f));
                _wallChannel.setPan(direction.Pan);
                _wallChannel.setPaused(false);
                _wallPlaying = true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[cue preview] wall demo failed: {e.Message}");
            }
        }

        private static void StopWallDemo()
        {
            if (!_wallPlaying) return;

            _wallPlaying = false;
            try
            {
                _wallChannel.stop();
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[cue preview] wall demo stop failed: {e.Message}");
            }

            _wallChannel = default;
        }
    }
}
