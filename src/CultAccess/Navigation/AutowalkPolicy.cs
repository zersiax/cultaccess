namespace CultAccess.Navigation
{
    /// <summary>
    /// The two judgements autowalk has to make that are not about the game: when the player
    /// has taken the wheel back, and when driving has stopped getting anywhere.
    ///
    /// Both are pure, so both are tested offline. That matters more here than elsewhere:
    /// these are the thresholds that decide whether the mod is holding a movement key the
    /// player cannot see it holding, and an in-game test of each boundary costs a launch.
    /// </summary>
    internal static class AutowalkPolicy
    {
        /// <summary>
        /// How much of the player's own movement input counts as taking over.
        ///
        /// Deliberately below the game's own <c>PlayerController.MinInputForMovement</c> of
        /// 0.3. Between the two figures the player is pushing and the game is not yet moving
        /// them; if autowalk kept driving through that band the character would set off in
        /// our direction while the player was leaning in another, which is the one behaviour
        /// that would make the feature feel like it was fighting them.
        /// </summary>
        internal const float ManualInputDeadzone = 0.15f;

        /// <summary>How long to allow no progress before giving the wheel back.</summary>
        internal const float StuckSeconds = 3f;

        /// <summary>
        /// How far counts as progress. The Lamb covers this in a fraction of a second at
        /// walking pace, so three seconds without it means something is in the way rather
        /// than that the route is slow.
        /// </summary>
        internal const float StuckDistance = 0.75f;

        /// <summary>
        /// Whether the player's own movement input should take priority this frame.
        ///
        /// Per axis rather than by vector length, matching how the game reads the same two
        /// numbers: a single axis held hard is unambiguous steering even though the pair is
        /// shorter than a diagonal.
        /// </summary>
        internal static bool PlayerIsSteering(float horizontal, float vertical) =>
            Abs(horizontal) > ManualInputDeadzone || Abs(vertical) > ManualInputDeadzone;

        private static float Abs(float value) => value < 0f ? -value : value;
    }

    /// <summary>
    /// Watches whether driving is actually moving the player, so walking into a wall ends in
    /// a sentence rather than in silence.
    ///
    /// A sighted player sees the Lamb pinned against scenery immediately. Ours cannot, and
    /// autowalk removes the one signal that would otherwise give it away — the feeling of
    /// holding a direction and getting nothing back. Without this the failure mode is a
    /// player standing still, believing they are en route, with nothing in the game saying
    /// otherwise.
    /// </summary>
    internal sealed class AutowalkProgress
    {
        private float _anchorTime;
        private float _anchorX;
        private float _anchorY;

        /// <summary>
        /// Restart the measurement here. Called whenever autowalk is not driving — a paused
        /// frame, a menu, or the player steering — so time spent legitimately still is never
        /// counted as being stuck.
        /// </summary>
        internal void Reset(float time, float x, float y)
        {
            _anchorTime = time;
            _anchorX = x;
            _anchorY = y;
        }

        /// <summary>
        /// Record where driving has got to. True exactly once each time the player has failed
        /// to cover <see cref="AutowalkPolicy.StuckDistance"/> within
        /// <see cref="AutowalkPolicy.StuckSeconds"/>.
        /// </summary>
        internal bool Observe(float time, float x, float y)
        {
            if (RoutePlanarMath.Distance(_anchorX, _anchorY, x, y) > AutowalkPolicy.StuckDistance)
            {
                Reset(time, x, y);
                return false;
            }

            if (time - _anchorTime < AutowalkPolicy.StuckSeconds) return false;

            // Re-anchored on the way out so a caller that ignores the verdict gets one report
            // per stuck interval rather than one per frame.
            Reset(time, x, y);
            return true;
        }
    }
}
