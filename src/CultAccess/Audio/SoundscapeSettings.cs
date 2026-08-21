namespace CultAccess.Audio
{
    /// <summary>
    /// Per-category tuning for the always-on layer: how far it reaches, how often it repeats,
    /// and how many things of that kind may sound at once.
    ///
    /// Kept separate from <see cref="CueSettings"/>, which owns on/off and volume for every
    /// cue including these. This holds only what is specific to a cue that repeats forever
    /// rather than firing once.
    /// </summary>
    internal class SoundscapeSettings
    {
        /// <summary>
        /// World units at which the category falls to silence. Defaults differ by category
        /// because they answer different questions: walls are about the space you are
        /// standing in, enemies are about what can reach you.
        /// </summary>
        internal float Radius;

        /// <summary>Seconds between repeats for one source.</summary>
        internal float Interval;

        /// <summary>
        /// How many of this category may sound per cycle, nearest first. The cap is what
        /// stops a busy room from turning the layer into noise, and it is per category so a
        /// crowded base does not drown out the one enemy behind you.
        /// </summary>
        internal int MaxSources;

        /// <summary>Falloff curve exponent. Higher keeps the category quiet until closer.</summary>
        internal float Falloff;

        internal SoundscapeSettings(float radius, float interval, int maxSources, float falloff)
        {
            Radius = radius;
            Interval = interval;
            MaxSources = maxSources;
            Falloff = falloff;
        }

        /// <summary>
        /// Defaults chosen so that every category, at its default radius, becomes audible at
        /// roughly the distance its name implies rather than at the radius itself.
        /// </summary>
        internal static SoundscapeSettings Default(CueId cue)
        {
            switch (cue)
            {
                // Orientation. Short reach and a steep curve, so an open room is silent and
                // a wall you are alongside is unmistakable.
                //
                // The probe rate has to beat walking pace, not merely feel responsive: at half
                // a second a player can cross a doorway between two probes and never hear the
                // gap that the whole feature exists to report. 0.15 puts two or three probes
                // inside a gap at ordinary speed.
                case CueId.AmbientWall:
                    return new SoundscapeSettings(5f, 0.15f, 8, 2.2f);

                case CueId.AmbientItem:
                    return new SoundscapeSettings(8f, 1f, 3, 2f);
                case CueId.AmbientInteractable:
                    return new SoundscapeSettings(8f, 1.2f, 3, 2f);
                case CueId.AmbientNpc:
                    return new SoundscapeSettings(10f, 1.5f, 3, 2f);

                // Reaches further and repeats faster than anything ambient: an enemy at the
                // edge of the room is worth knowing about before it is on top of you.
                case CueId.AmbientEnemy:
                    return new SoundscapeSettings(14f, 0.7f, 4, 1.6f);

                // The bullet-hell case. A dense stream of short ticks is the only way a
                // pattern of incoming fire reads as a pattern rather than as separate events,
                // so this repeats fastest and allows the most simultaneous sources.
                case CueId.AmbientProjectile:
                    return new SoundscapeSettings(12f, 0.25f, 6, 1.4f);

                default:
                    return new SoundscapeSettings(8f, 1f, 3, 2f);
            }
        }
    }
}
