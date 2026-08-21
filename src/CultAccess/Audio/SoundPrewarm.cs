using System.Collections.Generic;

namespace CultAccess.Audio
{
    /// <summary>
    /// Builds every sound ahead of time, one per frame, so no cue is ever created in the
    /// moment it was needed.
    ///
    /// Sounds are generated and handed to FMOD lazily on first play. The frame-budget log
    /// caught what that costs: single frames of 13 to 25 milliseconds attributed to the
    /// combat and soundscape scopes, which is a visible stutter, and it lands **the first
    /// time a given cue fires** — which is to say in the middle of the fight that needed it.
    /// That is also the right shape for a stutter that appears weapon by weapon, since a
    /// different weapon reaches a different cue for the first time.
    ///
    /// One sound per frame rather than all at once. Doing the lot in a single call would
    /// simply move the whole stall to load time instead of removing it, and this way each
    /// frame pays for one buffer of a few thousand samples and nothing else.
    ///
    /// Cheap to be wrong about: warming a sound that never plays wastes a little memory, and
    /// missing one only means the old lazy path handles it as before.
    /// </summary>
    internal static class SoundPrewarm
    {
        private static readonly Queue<Item> Pending = new Queue<Item>();

        private static bool _built;
        private static WallToneScheme _builtScheme;

        private struct Item
        {
            internal string Key;
            internal bool Looping;
            internal System.Func<float[]> Generate;
            internal string Variant;
        }

        /// <summary>Driven from the plugin's update. Does at most one sound per call.</summary>
        internal static void Tick()
        {
            // The wall ring's notes are baked per scheme, so a scheme change makes a fresh
            // set that has never been built. Re-queue rather than leave the new notes to be
            // created by the first wall the player walks up to.
            if (!_built || _builtScheme != WallSonar.Scheme) Build();

            if (Pending.Count == 0) return;

            var item = Pending.Dequeue();
            SoundBank.TryGet(item.Key, item.Looping, item.Generate, out _, item.Variant);
        }

        internal static void Reset()
        {
            Pending.Clear();
            _built = false;
        }

        private static void Build()
        {
            _built = true;
            _builtScheme = WallSonar.Scheme;
            Pending.Clear();

            foreach (var cue in Cues.All)
            {
                // The wall ring and the beacon do not use their cue-level sound; they have
                // their own per-direction and per-note files, queued below.
                if (cue == CueId.AmbientWall || cue == CueId.Beacon) continue;

                var id = cue;
                Pending.Enqueue(new Item
                {
                    Key = Cues.SoundFileName(id),
                    Looping = id == CueId.TimingWindow,
                    Generate = () => id == CueId.TimingWindow
                        ? Waveform.Loop(1180f, 8, 1f)
                        : CueLibrary.Generate(id),
                });
            }

            foreach (BeaconBand band in System.Enum.GetValues(typeof(BeaconBand)))
            {
                var value = band;
                Pending.Enqueue(new Item
                {
                    Key = BeaconTone.SoundKey(value),
                    Generate = () => BeaconTone.Generate(value),
                });
            }

            var scheme = _builtScheme;
            foreach (var direction in WallDirection.Ring)
            {
                var value = direction;
                Pending.Enqueue(new Item
                {
                    Key = value.SoundKey,
                    Looping = true,
                    Generate = () => value.Generate(scheme),
                    Variant = WallTones.Variant(scheme),
                });
            }

            Plugin.Log.LogInfo(
                $"[sound prewarm] queued {Pending.Count} sounds for scheme {scheme}, " +
                "one per frame");
        }
    }
}
