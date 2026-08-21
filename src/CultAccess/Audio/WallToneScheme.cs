namespace CultAccess.Audio
{
    /// <summary>How much of the wall ring's direction is carried by pitch.</summary>
    public enum WallToneScheme
    {
        /// <summary>A note per screen height. Five notes across the eight directions.</summary>
        AllNotes,

        /// <summary>One note everywhere. Direction comes entirely from spatial position.</summary>
        OneNote,

        /// <summary>One note for the directions with a side to them, one for straight ahead and behind.</summary>
        TwoNotes,

        /// <summary>High for up-screen, low for down-screen, middle for the sides.</summary>
        ThreeNotes,
    }

    /// <summary>
    /// Which note each direction of the wall ring sounds, under each scheme.
    ///
    /// The ring uses several notes because it has to: stereo panning cannot separate north
    /// from south at all — both sit dead centre — so pitch is the only channel left to carry
    /// that difference. A binaural spatialiser would have lifted the constraint, and one was
    /// built and then removed after it degraded the game's own audio; see `docs/game-api.md`.
    /// Panning is therefore the whole of the positional channel, and pitch is doing real work
    /// here rather than merely reinforcing it.
    ///
    /// How much of it is wanted is still a preference, so the schemes trade the same thing
    /// against each other in different proportions: how much redundancy is worth how much
    /// clutter.
    ///
    /// One property holds under **every** scheme: two directions at the same screen height
    /// always share a note, so left and right are never separated by pitch. Panning already
    /// does that job perfectly, and spending the pitch channel on it would waste it.
    ///
    /// The notes are a stack of perfect fourths. The first set was a pentatonic scale, which
    /// was pretty and too closely spaced to tell apart: its middle intervals collapsed to a
    /// major and then a minor third, so the directions that mattered most sat within three
    /// semitones of each other. Fourths are both wider and, more importantly, **uniform** —
    /// every adjacent pair is the same distance apart, so no two directions are harder to
    /// separate than any other two.
    ///
    /// A quartal stack also stays clean when several sound at once, which these do,
    /// continuously. It contains no semitones and no tritones, so there is nothing to beat
    /// however many directions are occupied.
    /// </summary>
    internal static class WallTones
    {
        // A stack of perfect fourths, each 4/3 above the one below: G3, C4, F4, B flat 4,
        // E flat 5. Spanning about an octave and two thirds, against the previous set's
        // octave and a bit.
        private const float Lowest = 196f;
        private const float Low = 261.63f;
        private const float Middle = 349.23f;
        private const float High = 466.16f;
        private const float Highest = 622.25f;

        /// <summary>Anything below this counts as no component on that axis.</summary>
        private const float AxisEpsilon = 0.01f;

        internal static float Frequency(WallDirection direction, WallToneScheme scheme)
        {
            switch (scheme)
            {
                // One steady texture, direction left entirely to panning. The cleanest and
                // the most demanding: panning cannot separate north from south, so under this
                // scheme they sound identical. Kept because a single texture is easier to
                // listen to for long stretches, and some players would rather read direction
                // from position alone.
                case WallToneScheme.OneNote:
                    return Middle;

                // Enough pitch to know whether a wall is off to a side or straight ahead or
                // behind, and no more. Front from back is left to panning, which cannot do it,
                // so this trades that distinction away for a quieter texture.
                case WallToneScheme.TwoNotes:
                    return HasSide(direction) ? Low : High;

                // Pitch spent only on the axis panning cannot express. Up-screen against
                // down-screen is the one pair panning collapses to the centre, so those take a
                // high and a low note; left and right are already separated perfectly and
                // share the middle one between them.
                //
                // Two fourths between each rather than one: with only three notes there is no
                // reason not to take the widest spread the set allows.
                case WallToneScheme.ThreeNotes:
                    return direction.Y > AxisEpsilon ? Highest
                        : direction.Y < -AxisEpsilon ? Lowest
                        : Middle;

                // A note per screen height, following the beacon's rule that higher means
                // further up. The most redundant and the most informative.
                default:
                    return direction.Y > 0.9f ? Highest
                        : direction.Y > AxisEpsilon ? High
                        : direction.Y < -0.9f ? Lowest
                        : direction.Y < -AxisEpsilon ? Low
                        : Middle;
            }
        }

        /// <summary>
        /// Whether a higher direction is guaranteed never to sound a *lower* note.
        ///
        /// Deliberately the weaker of the two possible promises. Only
        /// <see cref="WallToneScheme.AllNotes"/> gives every height its own note;
        /// <see cref="WallToneScheme.ThreeNotes"/> groups all the up-screen directions onto
        /// one note and all the down-screen ones onto another, so north and north-east sound
        /// the same. That is the scheme working, not failing — what would be a defect
        /// is an *inversion*, where something higher sounds lower, because that actively
        /// misleads a player who has learned the rule.
        ///
        /// <see cref="WallToneScheme.TwoNotes"/> cannot keep even that. Once north and south
        /// share a note and the diagonals take the other, a north-easterly wall sounds lower
        /// than one due south. There is no assignment of two notes to eight directions that
        /// avoids it, so it is the price of the scheme rather than something to fix.
        /// </summary>
        internal static bool NeverInvertsHeight(WallToneScheme scheme) =>
            scheme != WallToneScheme.TwoNotes;

        /// <summary>
        /// A token distinguishing one scheme's generated tones from another's in the sound
        /// cache. Without it, switching scheme would keep playing the notes already built.
        /// A file the player supplied is unaffected: it is played as recorded, whatever the
        /// scheme, because pitch-shifting someone's own sound eight ways would defeat the
        /// point of letting them supply it.
        /// </summary>
        internal static string Variant(WallToneScheme scheme) => scheme.ToString();

        private static bool HasSide(WallDirection direction) =>
            direction.X > AxisEpsilon || direction.X < -AxisEpsilon;
    }
}
