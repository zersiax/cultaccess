using CultAccess.Combat;
using FMODUnity;
using UnityEngine;

namespace CultAccess.Audio
{
    /// <summary>
    /// A continuous ring of quiet tones describing the solid geometry around the player.
    ///
    /// This is an orientation aid, and it is deliberately a different thing from the wall
    /// warning in <see cref="ObstacleSonar"/>. That one answers "will continuing in the
    /// direction I am already moving hit something?" and stays silent otherwise. This one
    /// answers "what shape is the space I am standing in?" and needs no input at all.
    ///
    /// **Continuous, not pulsed, and that distinction is the whole feature.** The first
    /// version played a repeating one-shot per direction, and it could not do the job it
    /// exists for: with a pulse train, a doorway is a beat that fails to arrive, and the ear
    /// cannot tell that apart from a beat that is merely late until several more have passed.
    /// With a sustained tone, walking past a doorway is a sound *stopping*, which is
    /// immediate and unmistakable. Following a wall and finding the gap in it is the exact
    /// task this has to support, so the gap has to be the salient event.
    ///
    /// Two consequences follow, and both are deliberate:
    ///
    /// - **No cap on how many directions may sound.** Every other always-on category limits
    ///   itself to the nearest few, because there is no cost to leaving a distant chair out.
    ///   Here there is: a direction culled to save noise is silent, and silent means "you can
    ///   walk that way". Culling would invent doorways. The clutter is controlled by the
    ///   falloff curve and a low volume ceiling instead.
    /// - **Volume is smoothed rather than stepped.** A tone that jumps in level on each probe
    ///   sounds like it is being retriggered, which reintroduces the pulse the feature exists
    ///   to avoid.
    /// </summary>
    internal static class WallSonar
    {
        /// <summary>Never mask a warning that means the player is about to be hit.</summary>
        private const float ThreatSuppressionSeconds = 0.3f;

        /// <summary>
        /// How fast a tone may rise, in full-scale volume per second. Gentle, so a wall coming
        /// into range fades in rather than snapping on.
        /// </summary>
        private const float RisePerSecond = 4f;

        /// <summary>
        /// How fast a tone may fall. Deliberately three times quicker than it rises, because
        /// the two directions mean different things: a tone appearing is a wall arriving, which
        /// nothing depends on being instant, while a tone *stopping* is a doorway, which is the
        /// event the whole ring exists to deliver. A symmetric slew spent the same 0.25 seconds
        /// blurring the edge of a gap as it did smoothing an onset nobody was waiting for.
        /// </summary>
        private const float FallPerSecond = 12f;

        /// <summary>
        /// Below this a channel is stopped outright rather than left running at zero. A
        /// direction that is inaudible must genuinely be silent, because silence is what the
        /// player reads as an opening.
        /// </summary>
        private const float StopBelow = 0.0015f;

        private static readonly FMOD.Channel[] Channels =
            new FMOD.Channel[WallDirection.Ring.Length];
        private static readonly bool[] Playing = new bool[WallDirection.Ring.Length];
        private static readonly float[] Levels = new float[WallDirection.Ring.Length];
        private static readonly float[] Targets = new float[WallDirection.Ring.Length];

        private static float _nextProbeAt;
        private static bool _failed;
        private static bool _loggedFirstTone;
        private static WallToneScheme _activeScheme;

        /// <summary>
        /// Which note each direction sounds. Changing it restarts every tone, because the
        /// notes are baked into the generated loops rather than applied as a pitch shift.
        /// </summary>
        /// <summary>
        /// How many directions to probe. Dropping to four leaves the diagonals to fade out
        /// through the ordinary slew rather than cutting them, so switching mid-play is not
        /// heard as a glitch.
        /// </summary>
        internal static WallRing Ring = WallRing.FourDirections;

        internal static WallToneScheme Scheme
        {
            get => _activeScheme;
            set
            {
                if (_activeScheme == value) return;

                _activeScheme = value;
                Reset();
                Plugin.Log.LogInfo($"[wall sonar] note scheme changed to {value}");
            }
        }

        /// <summary>Silence every direction. Safe to call when nothing is playing.</summary>
        internal static void Reset()
        {
            _nextProbeAt = 0f;

            for (var i = 0; i < Channels.Length; i++)
            {
                Targets[i] = 0f;
                Levels[i] = 0f;
                Stop(i);
            }
        }

        internal static void Tick(PlayerFarming player, SoundscapeSettings settings)
        {
            if (_failed || player == null || settings == null)
            {
                Reset();
                return;
            }

            Probe(player, settings);
            Apply();
        }

        /// <summary>
        /// Re-measure the geometry, at the configured rate rather than every frame. The tones
        /// keep sounding between probes; only the target levels are updated here.
        /// </summary>
        private static void Probe(PlayerFarming player, SoundscapeSettings settings)
        {
            if (Time.unscaledTime < _nextProbeAt) return;

            _nextProbeAt = Time.unscaledTime + Mathf.Max(0.05f, settings.Interval);

            // Stand aside while something is about to hit the player, but keep the tones
            // alive at their current level rather than cutting them, which would read as
            // every wall in the room disappearing at once.
            if (CuePlayer.CriticalRecently(ThreatSuppressionSeconds)) return;

            var collider = player.circleCollider2D;
            if (collider == null || !collider.enabled)
            {
                for (var i = 0; i < Targets.Length; i++) Targets[i] = 0f;
                return;
            }

            for (var i = 0; i < WallDirection.Ring.Length; i++)
            {
                var direction = WallDirection.Ring[i];

                if (Ring == WallRing.FourDirections && !direction.IsCardinal)
                {
                    Targets[i] = 0f;
                    continue;
                }

                if (!ObstacleProbe.TryFind(
                        player, collider, new Vector2(direction.X, direction.Y),
                        settings.Radius, out var hit))
                {
                    Targets[i] = 0f;
                    continue;
                }

                Targets[i] = SoundscapeFalloff.Volume(
                    hit.distance, settings.Radius, settings.Falloff);
            }
        }

        /// <summary>Move each tone toward its target level, starting and stopping as needed.</summary>
        private static void Apply()
        {
            var rise = RisePerSecond * Time.unscaledDeltaTime;
            var fall = FallPerSecond * Time.unscaledDeltaTime;

            for (var i = 0; i < Levels.Length; i++)
            {
                Levels[i] = Mathf.MoveTowards(
                    Levels[i], Targets[i], Targets[i] > Levels[i] ? rise : fall);

                var volume = CueSettings.ChannelVolume(CueId.AmbientWall, Levels[i]);
                if (volume < StopBelow)
                {
                    Stop(i);
                    continue;
                }

                if (!Playing[i] && !Start(i)) continue;

                Channels[i].setVolume(volume);
            }
        }

        private static bool Start(int index)
        {
            var direction = WallDirection.Ring[index];

            var scheme = _activeScheme;
            if (!SoundBank.TryGet(
                    direction.SoundKey, true, () => direction.Generate(scheme), out var sound,
                    WallTones.Variant(scheme)))
            {
                _failed = true;
                Plugin.Log.LogError(
                    $"[wall sonar] could not create the {direction.SoundKey} tone; wall tones disabled.");
                return false;
            }

            try
            {
                // Started paused so volume and pan are set before a single sample is heard;
                // otherwise every direction announces itself with a burst at full level.
                var result = RuntimeManager.CoreSystem.playSound(
                    sound, default(FMOD.ChannelGroup), true, out var channel);
                if (result != FMOD.RESULT.OK)
                {
                    Plugin.Log.LogWarning($"[wall sonar] {direction.SoundKey} playSound failed: {result}");
                    return false;
                }

                channel.setVolume(0f);
                channel.setPan(direction.Pan);

                // A random start offset, so eight loops of the same length do not lock into
                // phase with each other and beat.
                sound.getLength(out var length, FMOD.TIMEUNIT.PCM);
                if (length > 1) channel.setPosition((uint)Random.Range(0, (int)length), FMOD.TIMEUNIT.PCM);

                channel.setPaused(false);

                Channels[index] = channel;
                Playing[index] = true;

                if (!_loggedFirstTone)
                {
                    _loggedFirstTone = true;
                    Plugin.Log.LogInfo(
                        $"[wall sonar] first tone: {direction.SoundKey} pan={direction.Pan:0.00} " +
                        $"frequency={WallTones.Frequency(direction, scheme):0} scheme={scheme}");
                }

                return true;
            }
            catch (System.Exception e)
            {
                _failed = true;
                Plugin.Log.LogError($"[wall sonar] playback threw; wall tones disabled: {e}");
                return false;
            }
        }

        private static void Stop(int index)
        {
            if (!Playing[index]) return;

            Playing[index] = false;
            Levels[index] = 0f;

            try
            {
                Channels[index].stop();
            }
            catch (System.Exception e)
            {
                // A channel the system already reclaimed is not worth escalating, but a tone
                // that will not stop is the worst failure this feature has, so it is logged.
                Plugin.Log.LogWarning($"[wall sonar] stop failed: {e.Message}");
            }

            Channels[index] = default;
        }
    }
}
