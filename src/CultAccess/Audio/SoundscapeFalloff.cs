using System;

namespace CultAccess.Audio
{
    /// <summary>
    /// How an always-on cue loses volume with distance.
    ///
    /// This is the policy that decides whether the ambient layer is an orientation aid or an
    /// unusable wash. The requirement it exists to satisfy is specific: a wall should be
    /// essentially inaudible until the player is within a few units of it, so that hugging a
    /// wall and finding a gap in one are things you can hear rather than infer.
    ///
    /// A linear falloff does not do that — at half the radius it is still at half volume,
    /// which fills a room with everything in it. Raising the curve to a power concentrates
    /// the audible band near the object: at exponent 2 and a five-unit radius, something
    /// three units away plays at sixteen percent, and at one unit at sixty-four.
    ///
    /// Pure, so the curve can be asserted rather than guessed at from a play session.
    /// </summary>
    internal static class SoundscapeFalloff
    {
        /// <summary>
        /// Below this the cue is not worth a channel: it is inaudible under the game's own
        /// mix, and skipping it is what keeps a crowded room from spending sound channels on
        /// things the player cannot hear anyway.
        /// </summary>
        internal const float AudibleFloor = 0.02f;

        /// <summary>
        /// Volume scale from 0 at the radius to 1 at zero distance. Anything at or beyond
        /// the radius is silent, so the radius setting means exactly what it says.
        /// </summary>
        internal static float Volume(float distance, float radius, float exponent)
        {
            if (radius <= 0f) return 0f;
            if (distance <= 0f) return 1f;
            if (distance >= radius) return 0f;

            var near = 1f - distance / radius;
            if (exponent <= 1f) return near;

            return (float)Math.Pow(near, exponent);
        }

        internal static bool Audible(float volume) => volume >= AudibleFloor;

        /// <summary>
        /// The distance at which a category becomes audible, for the configuration menu.
        ///
        /// The radius alone is misleading — it is where the volume reaches zero, not where
        /// the player starts hearing anything — and a setting whose spoken value does not
        /// match what the player experiences is worse than no readout at all.
        /// </summary>
        internal static float AudibleDistance(float radius, float exponent)
        {
            if (radius <= 0f) return 0f;
            if (exponent <= 1f) return radius * (1f - AudibleFloor);

            return radius * (1f - (float)Math.Pow(AudibleFloor, 1.0 / exponent));
        }
    }
}
