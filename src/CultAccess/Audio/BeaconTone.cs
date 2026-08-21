using System;

namespace CultAccess.Audio
{
    /// <summary>Which of the beacon's three fixed notes a bearing sounds.</summary>
    internal enum BeaconBand
    {
        /// <summary>Ahead of the player: up-screen, which is north, which is W.</summary>
        Front,

        /// <summary>Off to one side. Panning already says which.</summary>
        Side,

        /// <summary>Behind the player: down-screen, which is south, which is S.</summary>
        Behind,
    }

    /// <summary>
    /// The beacon's pitch, as three fixed notes rather than a continuous slide.
    ///
    /// **Why this replaced a glide.** Pitch has no perceivable baseline. A listener hears
    /// *change* in pitch reliably and *absolute* pitch hardly at all, so a note sliding
    /// smoothly with bearing can say "you are drifting" but can never say "it is ahead of
    /// you" — there is nothing to compare against. The old beacon slid between 0.7 and 1.7
    /// times its base note, and the centre of that range was a value the player had no way to
    /// locate.
    ///
    /// Worse, the glide was mistaken for distance, and the way it misled was specific: walking
    /// straight at a target due north, the note falls steadily toward centre and behaves
    /// exactly like proximity. Approach the same target from due east and the note does not
    /// move at all while the distance collapses. Overshoot it and the note keeps travelling
    /// the same way it already was. It correlated on one heading, said nothing on another and
    /// inverted on a third.
    ///
    /// Three fixed notes are recognised rather than measured, which is the entire difference.
    /// Distance stays on the ping rate, which needs no baseline either — very fast is legible
    /// with nothing to compare it to.
    ///
    /// **Ahead means north, not "the way the Lamb is facing".** Movement in this game is
    /// world-absolute: `PlayerController` derives its movement direction straight from the
    /// input vector, so W is north whatever the Lamb did last, and facing is a consequence of
    /// moving rather than a steering mode. Anchoring the notes to facing would spin the whole
    /// soundscape every time the player tapped a direction, including taps that barely move
    /// them, and would then require translating "ahead" back into whichever key that was.
    /// Anchoring to north costs nothing to translate: ahead is W.
    ///
    /// This is not reasoning from the camera. The camera never rotates, so up-screen is a
    /// fixed world direction; the player only ever needs to know that W goes that way, which
    /// is a fact about the controls rather than about the view.
    /// </summary>
    internal static class BeaconTone
    {
        /// <summary>
        /// Half-width of the front and back sectors, in degrees. Ninety degrees each, leaving
        /// ninety to a side: wide enough that a target roughly ahead reads as ahead, rather
        /// than flickering between notes as the player strafes.
        /// </summary>
        private const float SectorHalfWidth = 45f;

        /// <summary>
        /// Base note, and a fifth either side of it. A fifth is far enough apart to be told
        /// apart instantly and consonant enough that the beacon does not sound like an alarm
        /// while it repeats.
        ///
        /// Three separate samples rather than one sample pitch-shifted, because shifting the
        /// pitch of a sample also changes its length: a ping shifted down an interval plays
        /// longer, and at close range, where pings come every seventh of a second, a longer
        /// ping runs into the next one and smears the distance channel it is competing with.
        /// </summary>
        private const float FrontFrequency = 1320f;
        private const float SideFrequency = 880f;
        private const float BehindFrequency = 587f;

        private const float PingSeconds = 0.075f;

        /// <summary>
        /// Choose a note from a screen-space offset. Pure, so the sector boundaries can be
        /// asserted rather than guessed at from play.
        /// </summary>
        internal static BeaconBand Band(float screenDeltaX, float screenDeltaY)
        {
            // Directly on top of the player has no bearing; treat it as a side rather than
            // letting floating-point noise pick a note.
            if (screenDeltaX == 0f && screenDeltaY == 0f) return BeaconBand.Side;

            var degrees = (float)(Math.Atan2(screenDeltaY, screenDeltaX) * 180.0 / Math.PI);

            if (Math.Abs(degrees - 90f) <= SectorHalfWidth) return BeaconBand.Front;
            if (Math.Abs(degrees + 90f) <= SectorHalfWidth) return BeaconBand.Behind;

            return BeaconBand.Side;
        }

        /// <summary>File stem under <c>sounds/</c>, so each note can be replaced separately.</summary>
        internal static string SoundKey(BeaconBand band)
        {
            switch (band)
            {
                case BeaconBand.Front: return "beacon-ahead";
                case BeaconBand.Behind: return "beacon-behind";
                default: return "beacon-side";
            }
        }

        internal static float Frequency(BeaconBand band)
        {
            switch (band)
            {
                case BeaconBand.Front: return FrontFrequency;
                case BeaconBand.Behind: return BehindFrequency;
                default: return SideFrequency;
            }
        }

        /// <summary>
        /// The note itself: the same burst shape at three frequencies, so the three read as
        /// one sound at different heights rather than as three different sounds.
        /// </summary>
        internal static float[] Generate(BeaconBand band) =>
            Waveform.Glide(PingSeconds, Frequency(band), Frequency(band), 1f);
    }
}
