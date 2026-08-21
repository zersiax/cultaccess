namespace CultAccess.Audio
{
    /// <summary>How many directions the wall ring probes.</summary>
    public enum WallRing
    {
        /// <summary>North, east, south and west only.</summary>
        FourDirections,

        /// <summary>The cardinals plus the four diagonals.</summary>
        EightDirections,
    }

    /// <summary>
    /// One direction of the wall ring: where to probe, where it sits in the stereo field, and
    /// what note represents it.
    ///
    /// Direction is carried by **pitch as well as pan**, which is the part that makes the ring
    /// legible. Pan alone cannot separate north from south at all — both are dead centre — and
    /// under headphones with the game's own audio running it separates the diagonals poorly.
    /// Giving each direction a fixed note means the player can name a direction from the sound
    /// itself, and it survives mono output, a bad stereo image, and hearing loss on one side.
    ///
    /// The mapping follows the convention the rest of the mod already uses and the player has
    /// already learned from the beacon: **higher is further up-screen, and pan is left to
    /// right**. North is the top note, south the bottom one, and the pairs that differ only in
    /// left/right share a note and differ only in pan.
    ///
    /// Which note each direction sounds is chosen by <see cref="WallTones"/>, and how much of
    /// the direction pitch carries at all is a player setting — see <see cref="WallToneScheme"/>.
    /// Every scheme draws from one C major pentatonic set, because these tones sound
    /// *simultaneously and continuously* and any dissonant interval would be present the whole
    /// time the player stands near a corner.
    /// </summary>
    internal struct WallDirection
    {
        /// <summary>
        /// Probe direction, as plain components rather than a Unity vector. That keeps the
        /// whole mapping - which note means which direction, and which side it is panned to -
        /// loadable by the offline harness, so it can be asserted rather than checked by ear.
        /// </summary>
        internal readonly float X;
        internal readonly float Y;

        internal readonly float Pan;

        /// <summary>File stem under <c>sounds/</c> for a player-supplied replacement.</summary>
        internal readonly string SoundKey;

        /// <summary>
        /// True for north, east, south and west: the directions with no component on one
        /// axis. The diagonals are what the four-direction ring leaves out.
        /// </summary>
        internal bool IsCardinal =>
            (X > -0.01f && X < 0.01f) || (Y > -0.01f && Y < 0.01f);

        private WallDirection(float x, float y, float pan, string soundKey)
        {
            X = x;
            Y = y;
            Pan = pan;
            SoundKey = soundKey;
        }

        /// <summary>Cycles per loop buffer. Enough to keep the buffer short but seamless.</summary>
        private const int LoopCycles = 16;

        /// <summary>Sine plus a quiet octave, for presence at very low volume.</summary>
        private static readonly float[] Harmonics = { 1f, 0.22f };

        private const float Diagonal = 0.7071f;

        /// <summary>
        /// The ring, ordered clockwise from north, with the cardinals on the even indices.
        ///
        /// Whether all eight are used is a player setting, because the trade is real and runs
        /// both ways. Most dungeon geometry is axis-aligned, so a wall to the west is usually
        /// hit by the north-west and south-west probes as well, about 1.4 times further away
        /// — the same wall reported three times at three volumes rather than three facts. And
        /// following that wall to a doorway, the diagonals go quiet just before and just after
        /// the cardinal does, turning one clean transition into a smeared three-stage fade,
        /// which works against the thing the ring exists for.
        ///
        /// Where the diagonals earn their place is geometry that is not axis-aligned, and
        /// inside corners, where a diagonal probe finds something neither neighbour does. How
        /// much of that this game actually contains is a level-design question the decompile
        /// cannot answer, so it is settled by ear rather than by argument.
        /// </summary>
        internal static readonly WallDirection[] Ring =
        {
            new WallDirection(0f, 1f, 0f, "wall-north"),
            new WallDirection(Diagonal, Diagonal, 0.7f, "wall-northeast"),
            new WallDirection(1f, 0f, 1f, "wall-east"),
            new WallDirection(Diagonal, -Diagonal, 0.7f, "wall-southeast"),
            new WallDirection(0f, -1f, 0f, "wall-south"),
            new WallDirection(-Diagonal, -Diagonal, -0.7f, "wall-southwest"),
            new WallDirection(-1f, 0f, -1f, "wall-west"),
            new WallDirection(-Diagonal, Diagonal, -0.7f, "wall-northwest"),
        };

        /// <summary>
        /// The built-in tone for this direction under a given scheme, used when the player
        /// has supplied no file. Generated at the direction's own frequency rather than
        /// pitch-shifted from one sample, so a replacement file plays exactly as recorded.
        /// </summary>
        internal float[] Generate(WallToneScheme scheme) =>
            Waveform.Loop(WallTones.Frequency(this, scheme), LoopCycles, 0.9f, Harmonics);
    }
}
