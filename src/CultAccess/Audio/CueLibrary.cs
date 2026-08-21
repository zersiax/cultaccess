namespace CultAccess.Audio
{
    /// <summary>
    /// The waveform of every cue, in one place, with the reasoning for each choice.
    ///
    /// Kept free of Unity and FMOD so the offline harness can assert the things that decide
    /// whether a cue works in practice and that no play session can prove: that it does not
    /// clip, that it begins and ends at silence, and above all that cues which must be told
    /// apart are actually built from different material.
    ///
    /// That last check exists because of a real defect. The first vocabulary described the
    /// dodge cue as a 650 to 1350 Hz rise and the evade confirmation as a 700 to 1450 Hz
    /// rise: two sweeps within a few percent of each other, in the same register, with the
    /// same envelope and the same harmonic content. They were reported as indistinguishable,
    /// and they were — the code says so plainly once the numbers are next to each other.
    ///
    /// So the rule now is that no two cues share both material and rhythm:
    ///
    /// - Filtered noise is used by the dodge cues and nothing else.
    /// - Inharmonic metallic strikes are used by the static-trap warning and nothing else.
    /// - The evade confirmation is two discrete notes, never a glide.
    /// </summary>
    internal static class CueLibrary
    {
        /// <summary>
        /// Sine plus a quiet octave. The harmonic signature the first cue set used, kept for
        /// every cue that was already tested and reported as working.
        /// </summary>
        private static readonly float[] ToneHarmonics = { 1f, 0.18f };

        /// <summary>A fuller stack for the ambient categories, which play quietly.</summary>
        private static readonly float[] AmbientHarmonics = { 1f, 0.3f, 0.12f };

        /// <summary>Odd partials only: a hollow, woody timbre for ambient NPCs.</summary>
        private static readonly float[] HollowHarmonics = { 1f, 0f, 0.28f, 0f, 0.1f };

        internal static float[] Generate(CueId cue)
        {
            switch (cue)
            {
                // Wayfinding. A short sine burst; pan, pitch and repeat rate carry the
                // information, so the timbre itself is deliberately plain.
                case CueId.Beacon:
                    return Waveform.Glide(0.075f, 880f, 880f, 1f);

                // Solid geometry ahead. Low and falling, so it sits under speech.
                case CueId.WallNear:
                    return Waveform.Glide(0.09f, 430f, 310f, 0.82f, ToneHarmonics);
                case CueId.WallBlocked:
                    return Waveform.Impact(0.18f, 240f, 95f, 0.9f);

                // Dodge. Noise, not tone: the previous glide was mistaken for the evade
                // confirmation, and a transient with no pitch cannot be mistaken for a note.
                // A hard click leads it in so the onset is unmissable under weapon audio.
                case CueId.DodgeDirection:
                    return DodgeBurst();
                case CueId.DodgeBlocked:
                    return Waveform.Then(DodgeBurst(), Waveform.Impact(0.13f, 260f, 100f, 0.95f),
                        0.025f);

                // Incoming attacks. Two pulses for anything ranged; melee is the growl.
                //
                // Melee is the one warning where the threat is already within reach, so it
                // gets the most aggressive material in the set. The previous smooth sine
                // descent was reported as far too quiet and unassuming for its meaning, and
                // that was a fault of timbre rather than level: a pure tone reads as an
                // announcement however loud it is.
                case CueId.MeleeThreat:
                    return Waveform.Growl(0.22f, 880f, 240f, 0.92f, 30f, 0.4f);
                //
                // Both are growls too. They mean exactly what the melee wind-up means — you
                // are about to be hit, move — and leaving them as plain sine glides made two
                // thirds of the mod's danger vocabulary sound like an announcement. The
                // doubled pulse and the register still say which is which.
                case CueId.ProjectileThreat:
                    return DoubleGrowl(0.07f, 1550f, 850f);
                case CueId.AreaThreat:
                    return DoubleGrowl(0.075f, 500f, 760f);

                // Static trap. The only rattle in the vocabulary, and the only inharmonic
                // material. Six accelerating metallic strikes inside 0.26 seconds, which
                // fits the trap's own 0.5 second telegraph with reaction time to spare.
                case CueId.StaticTrap:
                    return Waveform.Layer(
                        Waveform.Rattle(0.26f, 6, 1240f, 0.62f),
                        Waveform.Impact(0.1f, 430f, 210f, 0.5f),
                        0f);

                // Evade confirmation. Two discrete rising notes rather than a glide, so it
                // is separated from the dodge cue by rhythm as well as by material.
                case CueId.DodgeAvoidedHit:
                    return Waveform.Steps(0.055f, new[] { 660f, 990f }, 0.78f, 0.012f,
                        ToneHarmonics);

                // Minigame timing. The sustained window tone is generated by TimingTone,
                // which needs a seamless loop; this is the fixed-length form the learn
                // sounds walkthrough demonstrates.
                case CueId.TimingWindow:
                    return Waveform.Glide(0.4f, 1180f, 1180f, 0.8f);
                case CueId.TimingChirp:
                    return Waveform.Glide(0.065f, 950f, 1450f, 0.9f);

                // Ambient categories. Short, soft and each a different material, because
                // several of them can be audible at once and the player has to hear the mix
                // as a scene rather than as a queue.
                // Only a stand-in. The real wall ring is eight sustained loops, one per
                // direction, generated by WallDirection at its own frequency; this exists so
                // the cue still has a waveform like every other entry in the list.
                case CueId.AmbientWall:
                    return Waveform.Glide(0.11f, 180f, 165f, 0.7f, AmbientHarmonics);
                case CueId.AmbientItem:
                    return Waveform.Steps(0.045f, new[] { 1320f }, 0.7f, 0f, ToneHarmonics);
                case CueId.AmbientInteractable:
                    return Waveform.Glide(0.1f, 590f, 590f, 0.62f, AmbientHarmonics);
                case CueId.AmbientNpc:
                    return Waveform.Glide(0.13f, 330f, 345f, 0.62f, HollowHarmonics);
                case CueId.AmbientEnemy:
                    return Waveform.Layer(
                        Waveform.Glide(0.12f, 220f, 196f, 0.6f, AmbientHarmonics),
                        Waveform.Rattle(0.09f, 2, 620f, 0.22f),
                        0.01f);
                case CueId.AmbientProjectile:
                    return Waveform.Glide(0.035f, 1760f, 1760f, 0.66f);

                default:
                    return Waveform.Glide(0.1f, 600f, 600f, 0.75f);
            }
        }

        /// <summary>
        /// Per-cue trim applied before the player's own volume.
        ///
        /// These are perceptual corrections, not preferences: a low pulse and a high tick at
        /// the same amplitude are not equally audible under the game's own mix. The player's
        /// volume setting multiplies whatever this returns, so raising a cue to full still
        /// respects the relative balance.
        /// </summary>
        internal static float AudibilityGain(CueId cue)
        {
            switch (cue)
            {
                // The beacon applies its own distance curve and was tuned by ear at unity,
                // so it must not be trimmed here. Anything else would change a sound the
                // player has already learned.
                case CueId.Beacon:
                    return 1f;

                case CueId.WallNear:
                    return 1.3f;
                case CueId.WallBlocked:
                case CueId.DodgeBlocked:
                    return 1.4f;

                // Deliberately the quietest thing in the set. It reports where a dodge took
                // you, after the fact, and nothing depends on reacting to it — so it has no
                // business being as loud as a warning. The first version was a hard snap,
                // which came from over-correcting a real problem: it needed to be *distinct*
                // from the evade confirmation, and distinctness is a matter of material
                // rather than of volume.
                case CueId.DodgeDirection:
                    return 1.2f;
                // Every cue that means "you are about to take damage", at one level. They
                // ask for the same thing from the player and there is no reason for one of
                // them to be easier to miss than another.
                case CueId.StaticTrap:
                case CueId.MeleeThreat:
                case CueId.ProjectileThreat:
                case CueId.AreaThreat:
                    return 1.9f;

                // Trimmed harder than anything else in the set, because it does not repeat -
                // it sustains, for as long as the player is near a wall. The reference
                // implementation this was modelled on caps its equivalent at 0.14 of full
                // scale; combined with the default volume this lands in the same place.
                case CueId.AmbientWall:
                    return 0.35f;

                // Ambient cues repeat for as long as something is nearby, so they are trimmed
                // well below the event cues. They are meant to sit under everything else.
                case CueId.AmbientItem:
                case CueId.AmbientInteractable:
                case CueId.AmbientNpc:
                case CueId.AmbientEnemy:
                case CueId.AmbientProjectile:
                    return 0.85f;

                default:
                    return 1.4f;
            }
        }

        /// <summary>
        /// A soft band of filtered noise, swelling rather than snapping.
        ///
        /// Noise because nothing else in the vocabulary uses it, which is what keeps this
        /// from being confused with the evade confirmation. Soft because the cue is
        /// *informational*: it says where a dodge took you, after it has already happened,
        /// and there is nothing to react to. An earlier version layered a hard transient on
        /// top and ran at warning volume, which made a report sound like an alarm.
        /// </summary>
        private static float[] DodgeBurst() =>
            Waveform.NoiseBurst(0.11f, 600f, 3200f, 0.5f, attackFraction: 0.16f);

        /// <summary>
        /// Two growls in quick succession, for the ranged threats. Built from two passes
        /// rather than a gated envelope so each half keeps the hard onset that makes a growl
        /// read as a growl.
        /// </summary>
        private static float[] DoubleGrowl(float seconds, float from, float to)
        {
            var pulse = Waveform.Growl(seconds, from, to, 0.88f, 34f, 0.32f);
            return Waveform.Then(pulse, pulse, 0.03f);
        }
    }
}
