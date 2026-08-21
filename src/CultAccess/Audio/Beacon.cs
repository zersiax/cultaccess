using System;
using System.Runtime.InteropServices;
using FMODUnity;
using UnityEngine;

namespace CultAccess.Audio
{
    /// <summary>
    /// A repeating audio ping that encodes where something is, so the player can turn
    /// toward it without waiting for a spoken update.
    ///
    /// Two channels carry the two axes, because stereo alone cannot express a 2D bearing:
    ///
    /// - **Pan** carries left/right.
    /// - **Pitch** carries up/down — higher is up-screen (north), lower is down-screen.
    /// - **Ping rate** carries distance: faster as you close in.
    ///
    /// Positions come from the camera projection, matching <see cref="Navigation.Compass"/>,
    /// so the beacon and the spoken bearings always agree.
    ///
    /// Plays through **FMOD**, not Unity audio. The first attempt used an AudioSource and was
    /// silent: the diagnostic reported <c>clipSamples=0</c> and <c>isPlaying=False</c>, which
    /// is what happens when a project ships with Unity audio disabled — as an FMOD-only title
    /// naturally would. Unity will hand out AudioClips and AudioSources that never make a
    /// sound. FMOD is demonstrably running here, since it is what the game plays through.
    ///
    /// A discrete ping rather than a continuous tone: a sustained drone under combat audio is
    /// fatiguing and masks the game's own cues, which are what we still need to hear.
    /// </summary>
    public static class Beacon
    {
        private const int SampleRate = 44100;
        private const float PingSeconds = 0.075f;
        private const float PingFrequency = 880f;

        /// <summary>Beyond this distance the beacon pings at its slowest rate.</summary>
        private const float FarDistance = 30f;

        private const float FastestInterval = 0.14f;
        private const float SlowestInterval = 0.85f;

        private static bool _soundFailed;

        // Navigation and enemy tracking share one sound but not one target slot. Enemy
        // tracking takes priority while active; ending it reveals the still-current route.
        private static readonly BeaconTarget NavigationTarget = new BeaconTarget();
        private static readonly BeaconTarget EnemyTarget = new BeaconTarget();
        private static float _nextPingAt;
        private static bool _loggedFirstPing;

        /// <summary>
        /// Read from the shared cue settings rather than held here, so the beacon appears in
        /// the settings menu and the learn-sounds walkthrough alongside every other sound
        /// instead of being a special case the player has to find separately.
        /// </summary>
        public static bool Enabled => CueSettings.Enabled(CueId.Beacon);

        public static float Volume => CueSettings.Volume(CueId.Beacon);

        public static bool Active => EnemyTarget.Active || NavigationTarget.Active;

        /// <summary>
        /// Follow a moving transform. The offset is world-space because that is how the
        /// game's <c>Interaction.ActivatorOffset</c> is applied.
        /// </summary>
        internal static void SetNavigationTarget(
            Transform target,
            Vector3 offset = default(Vector3))
        {
            CaptureEffectiveTarget(out var hadPrevious, out var previous, out var previousMode);
            if (!NavigationTarget.SetFollow(target, offset)) return;
            OnEffectiveTargetChanged(hadPrevious, previous, previousMode);
        }

        /// <summary>Point at a fixed world position, such as the next route waypoint.</summary>
        internal static void SetNavigationPosition(Vector3 position)
        {
            CaptureEffectiveTarget(out var hadPrevious, out var previous, out var previousMode);
            if (!NavigationTarget.SetPosition(position)) return;
            OnEffectiveTargetChanged(hadPrevious, previous, previousMode);
        }

        internal static void SetEnemyTarget(Transform target)
        {
            CaptureEffectiveTarget(out var hadPrevious, out var previous, out var previousMode);
            if (!EnemyTarget.SetFollow(target, Vector3.zero)) return;
            OnEffectiveTargetChanged(hadPrevious, previous, previousMode);
        }

        internal static void ClearNavigation() => ClearTarget(NavigationTarget);

        internal static void ClearEnemy() => ClearTarget(EnemyTarget);

        internal static void ClearAll()
        {
            CaptureEffectiveTarget(out var hadPrevious, out var previous, out var previousMode);
            var changed = EnemyTarget.Clear() | NavigationTarget.Clear();
            if (changed) OnEffectiveTargetChanged(hadPrevious, previous, previousMode);
        }

        internal static void Shutdown()
        {
            ClearAll();

            // The sounds themselves belong to SoundBank, which owns every mod sound's
            // lifetime and releases them together.
            _soundFailed = false;
            _loggedFirstPing = false;
        }

        internal static bool TryGetTargetPosition(out Vector3 position, out string mode)
        {
            if (EnemyTarget.TryGetPosition(out position, out var enemyFollows))
            {
                mode = enemyFollows ? "enemy-transform" : "enemy-fixed";
                return true;
            }

            if (NavigationTarget.TryGetPosition(out position, out var navigationFollows))
            {
                mode = navigationFollows ? "navigation-transform" : "navigation-fixed";
                return true;
            }

            position = Vector3.zero;
            mode = "none";
            return false;
        }

        private static void ClearTarget(BeaconTarget target)
        {
            CaptureEffectiveTarget(out var hadPrevious, out var previous, out var previousMode);
            if (!target.Clear()) return;
            OnEffectiveTargetChanged(hadPrevious, previous, previousMode);
        }

        private static void CaptureEffectiveTarget(
            out bool exists,
            out Vector3 position,
            out string mode)
        {
            exists = TryGetTargetPosition(out position, out mode);
        }

        private static void OnEffectiveTargetChanged(
            bool hadPrevious,
            Vector3 previous,
            string previousMode)
        {
            var hasCurrent = TryGetTargetPosition(out var current, out var currentMode);
            if (hasCurrent && hadPrevious && currentMode == previousMode && current == previous)
                return;

            _nextPingAt = 0f;
            if (hasCurrent)
            {
                Diagnostics.NavigationDiagnostics.LogBeaconTargetChange(
                    currentMode, current, hadPrevious ? previous : (Vector3?)null);
            }
            else if (hadPrevious && Diagnostics.NavigationDiagnostics.Enabled)
            {
                Plugin.Log.LogInfo($"[beacon target] owner={previousMode} cleared");
            }
        }

        /// <summary>Driven from the plugin's Update. Cheap when no target is set.</summary>
        public static void Tick()
        {
            if (!Enabled || !Active || _soundFailed) return;

            var listener = Navigation.NavigatorPlayer.Resolve();
            if (listener == null) return;

            var camera = Camera.main;
            if (camera == null) return;

            if (Time.unscaledTime < _nextPingAt) return;

            if (!TryGetTargetPosition(out var targetPosition, out _)) return;
            var from = camera.WorldToScreenPoint(listener.position);
            var to = camera.WorldToScreenPoint(targetPosition);

            // Normalise against a fraction of the screen so nearby offsets still register
            // strongly rather than sitting near dead centre.
            var deltaX = to.x - from.x;
            var deltaY = to.y - from.y;
            var pan = Mathf.Clamp(deltaX / (Screen.width * 0.35f), -1f, 1f);

            var distance = Vector3.Distance(listener.position, targetPosition);
            var closeness = 1f - Mathf.Clamp01(distance / FarDistance);

            // Ahead, behind or to a side, as one of three fixed notes. Never a slide: see
            // BeaconTone for why a continuously varying pitch cannot say where something is.
            var band = BeaconTone.Band(deltaX, deltaY);
            var volume = Mathf.Clamp01(Volume) * Mathf.Lerp(0.45f, 1f, closeness);

            _nextPingAt = Time.unscaledTime + Mathf.Lerp(SlowestInterval, FastestInterval, closeness);
            Play(band, pan, volume, deltaX, deltaY);
        }

        private static void Play(
            BeaconBand band, float pan, float volume, float deltaX, float deltaY)
        {
            try
            {
                var system = RuntimeManager.CoreSystem;
                if (!TryGetSound(band, out var sound)) return;

                // Fire and forget: each ping is its own short channel, so its position is
                // fixed at trigger time and never needs per-frame updating.
                var result = system.playSound(sound, default(FMOD.ChannelGroup), true, out var channel);
                if (result != FMOD.RESULT.OK)
                {
                    Plugin.Log.LogWarning($"Beacon playSound failed: {result}");
                    return;
                }

                channel.setVolume(volume);
                channel.setPan(pan);
                channel.setPaused(false);

                if (!_loggedFirstPing)
                {
                    _loggedFirstPing = true;
                    Plugin.Log.LogInfo(
                        $"[beacon] first ping band={band} pan={pan:0.00} volume={volume:0.00}");
                }
            }
            catch (Exception e)
            {
                _soundFailed = true;
                Plugin.Log.LogError($"Beacon playback threw, disabling beacon: {e}");
            }
        }

        /// <summary>
        /// Resolve one of the three notes, preferring a file the player supplied in
        /// <c>sounds/</c> over the generated burst. See <see cref="SoundBank"/>.
        /// </summary>
        private static bool TryGetSound(BeaconBand band, out FMOD.Sound sound)
        {
            if (SoundBank.TryGet(
                    BeaconTone.SoundKey(band), false,
                    () => BeaconTone.Generate(band), out sound))
                return true;

            _soundFailed = true;
            Plugin.Log.LogError("Beacon sound could not be created. Beacon disabled.");
            return false;
        }


    }
}
