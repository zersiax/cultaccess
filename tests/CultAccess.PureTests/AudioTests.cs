using System;
using CultAccess.Audio;

/// <summary>
/// Properties of the generated cue set that decide whether it works in practice and that a
/// play session cannot establish.
///
/// A tester can say "I could not tell those apart", which is how the last defect was found,
/// but not "these two waveforms are 99 percent correlated" — and the second statement is the
/// one that says what to change. These assertions turn the audible symptom into a number.
/// </summary>
internal static class AudioTests
{
    /// <summary>
    /// Measured across the whole set: every unrelated pair sits at or below 0.18, and the
    /// only pair above it is the dodge pair, which is deliberately related. Half is therefore
    /// a wide margin that still fails immediately if two cues converge again.
    /// </summary>
    private const double MaximumUnrelatedCorrelation = 0.5;

    internal static void Run(Action<bool, string> assert)
    {
        // The hand-written cue list is what gives every cue a setting, a menu row and a
        // lesson. A cue missing from it would simply never sound, with nothing to see.
        var unlisted = "";
        assert(
            Cues.Validate(message => unlisted += message),
            "every cue must be listed in Cues.All: " + unlisted);

        foreach (var cue in Cues.All)
        {
            var samples = CueLibrary.Generate(cue);

            assert(samples.Length > 0, $"cue {cue} must generate audio");

            var peak = 0f;
            foreach (var sample in samples)
                if (Math.Abs(sample) > peak)
                    peak = Math.Abs(sample);

            assert(peak <= 1f, $"cue {cue} must not clip");
            assert(peak > 0.1f, $"cue {cue} must be loud enough to hear");

            // A buffer that starts or ends away from zero produces an audible click on every
            // play. With cues that fire several times a second, that click becomes the loudest
            // part of the mod.
            assert(
                Math.Abs(samples[0]) < 0.001f && Math.Abs(samples[samples.Length - 1]) < 0.001f,
                $"cue {cue} must begin and end at silence so it cannot click");
        }

        AssertDistinct(assert);
        AssertDodgeIsNotEvade(assert);
        AssertMeleeIsUrgent(assert);
        AssertTrapFitsItsTelegraph(assert);
        AssertFalloff(assert);
        AssertWallRing(assert);
        AssertSoundFileNames(assert);
        AssertMeleeTiming(assert);
        AssertBeaconBands(assert);
    }

    /// <summary>
    /// The melee wind-up was reported as far too quiet and unassuming for what it means. The
    /// fix was material rather than level, so the test is about material: a dense harmonic
    /// stack spreads energy across the spectrum and cuts through a mix that a bare sine does
    /// not, at the same peak amplitude.
    /// </summary>
    private static void AssertMeleeIsUrgent(Action<bool, string> assert)
    {
        var melee = CueLibrary.Generate(CueId.MeleeThreat);

        assert(
            Rms(melee) > Rms(CueLibrary.Generate(CueId.ProjectileThreat)),
            "the melee wind-up must carry more energy than the ranged warnings; the threat is " +
            "already within reach by the time it fires");

        assert(
            CueLibrary.AudibilityGain(CueId.MeleeThreat) >
            CueLibrary.AudibilityGain(CueId.WallNear),
            "the melee wind-up must be trimmed louder than an ambient wall pulse");

        // Every cue meaning "you are about to be hit" is trimmed alike. They ask the same
        // thing of the player, so none of them may be easier to miss than its siblings.
        foreach (var threat in new[]
                 { CueId.ProjectileThreat, CueId.AreaThreat, CueId.StaticTrap })
            assert(
                Math.Abs(CueLibrary.AudibilityGain(threat) -
                         CueLibrary.AudibilityGain(CueId.MeleeThreat)) < 0.001f,
                $"{threat} means the same thing as the melee wind-up and must be trimmed to " +
                "match it, or one warning is quieter than another for no reason");

        // The dodge report is the counterweight: it says where a dodge took you once it has
        // happened, so it must stay well below anything the player has to react to. This is
        // the assertion that stops a future "make it clearer" from turning a report back
        // into an alarm.
        assert(
            CueLibrary.AudibilityGain(CueId.DodgeDirection) <
            CueLibrary.AudibilityGain(CueId.MeleeThreat) * 0.75f,
            "the dodge direction report must stay clearly quieter than a damage warning");

        // A held level rather than a percussive decay: the warning has to stay present for
        // the whole wind-up rather than fading out during it.
        var firstHalf = Rms(melee, 0, melee.Length / 2);
        var secondHalf = Rms(melee, melee.Length / 2, melee.Length);
        assert(
            secondHalf > firstHalf * 0.5f,
            "the melee wind-up must hold its level rather than decaying away before the " +
            "attack actually lands");
    }

    /// <summary>
    /// The wall ring is the one place where the mapping from direction to sound *is* the
    /// feature, so it is asserted rather than trusted: eight unit directions, pan following
    /// left and right, and pitch following up and down exactly as the beacon already taught.
    /// </summary>
    private static void AssertWallRing(Action<bool, string> assert)
    {
        var ring = WallDirection.Ring;
        assert(ring.Length == 8, "the wall ring must probe eight directions");

        var keys = new System.Collections.Generic.HashSet<string>();
        foreach (var direction in ring)
        {
            assert(
                keys.Add(direction.SoundKey),
                $"wall direction sound names must be unique; {direction.SoundKey} repeats");

            var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
            assert(
                Math.Abs(length - 1.0) < 0.01,
                $"wall direction {direction.SoundKey} must be a unit vector, or it probes a " +
                "different distance than the rest of the ring");

            assert(
                Math.Sign(direction.Pan) == Math.Sign(direction.X),
                $"wall direction {direction.SoundKey} must pan to the side it points at");
        }

        AssertCardinals(assert, ring);

        foreach (WallToneScheme scheme in Enum.GetValues(typeof(WallToneScheme)))
            AssertScheme(assert, ring, scheme);
    }

    /// <summary>
    /// The four-direction ring drops exactly the diagonals, and keeps a direction on every
    /// axis. Getting this wrong would silence a whole side of the player without anything to
    /// see: a direction that is never probed is silent, and silent is how the ring says
    /// "you can walk that way".
    /// </summary>
    private static void AssertCardinals(Action<bool, string> assert, WallDirection[] ring)
    {
        var cardinals = 0;
        foreach (var direction in ring)
        {
            if (!direction.IsCardinal) continue;

            cardinals++;
            assert(
                Math.Abs(direction.X) < 0.01f || Math.Abs(direction.Y) < 0.01f,
                $"{direction.SoundKey} is counted as a cardinal but points diagonally");
        }

        assert(
            cardinals == 4,
            $"the four-direction ring must keep exactly four probes, not {cardinals}");

        // One per side, or the reduced ring has a blind spot rather than merely fewer probes.
        var up = 0;
        var down = 0;
        var left = 0;
        var right = 0;
        foreach (var direction in ring)
        {
            if (!direction.IsCardinal) continue;

            if (direction.Y > 0.5f) up++;
            if (direction.Y < -0.5f) down++;
            if (direction.X < -0.5f) left++;
            if (direction.X > 0.5f) right++;
        }

        assert(
            up == 1 && down == 1 && left == 1 && right == 1,
            $"the four-direction ring must cover every side exactly once " +
            $"(up {up}, down {down}, left {left}, right {right})");
    }

    /// <summary>
    /// Each note scheme, held to what it actually claims. Two of the four deliberately give
    /// up the height-to-pitch rule in exchange for fewer simultaneous notes, so asserting it
    /// across the board would fail them for doing their job.
    /// </summary>
    private static void AssertScheme(
        Action<bool, string> assert,
        WallDirection[] ring,
        WallToneScheme scheme)
    {
        // Universal, whatever the scheme: two directions at the same screen height share a
        // note. Panning already separates left from right perfectly, so spending the pitch
        // channel on it would waste the one channel the schemes are rationing.
        foreach (var first in ring)
        foreach (var second in ring)
        {
            if (Math.Abs(first.Y - second.Y) >= 0.01f) continue;

            assert(
                Math.Abs(WallTones.Frequency(first, scheme) -
                         WallTones.Frequency(second, scheme)) < 0.01f,
                $"under {scheme}, {first.SoundKey} and {second.SoundKey} are at the same " +
                "height and must share a note, leaving pan to carry left and right");
        }

        // The promise is that a higher wall never sounds *lower*, not that every height gets
        // its own note. Grouping heights onto a shared note is a scheme doing its job;
        // inverting them would mislead a player who has learned that higher means up-screen.
        if (WallTones.NeverInvertsHeight(scheme))
        {
            foreach (var first in ring)
            foreach (var second in ring)
            {
                if (first.Y <= second.Y) continue;

                assert(
                    WallTones.Frequency(first, scheme) >= WallTones.Frequency(second, scheme),
                    $"under {scheme}, {first.SoundKey} is higher up-screen than " +
                    $"{second.SoundKey} but sounds a lower note, which inverts the rule the " +
                    "beacon already taught the player");
            }
        }

        AssertRingIsConsonant(assert, ring, scheme);

        foreach (var direction in ring)
            AssertLoopsSeamlessly(
                assert, $"{direction.SoundKey} ({scheme})", direction.Generate(scheme));
    }

    /// <summary>
    /// These tones sound together and continuously, so any two that are close but not equal
    /// would beat against each other for as long as the player stands near a corner.
    /// </summary>
    private static void AssertRingIsConsonant(
        Action<bool, string> assert,
        WallDirection[] ring,
        WallToneScheme scheme)
    {
        // A perfect fourth, less a little slack for the tempered frequencies. The first set
        // was merely consonant and was reported as hard to tell apart, its middle intervals
        // having narrowed to a minor third; this pins the wider spacing so it cannot drift
        // back. Checked for every scheme, since the notes sound together and continuously.
        const double MinimumRatio = 1.3;

        foreach (var first in ring)
        foreach (var second in ring)
        {
            var low = WallTones.Frequency(first, scheme);
            var high = WallTones.Frequency(second, scheme);
            if (low >= high) continue;

            var ratio = high / low;
            assert(
                ratio >= MinimumRatio,
                $"under {scheme}, {first.SoundKey} and {second.SoundKey} are too close in " +
                $"pitch to sound at the same time without beating (ratio {ratio:0.000})");
        }
    }

    /// <summary>
    /// A looping tone whose ends do not meet clicks on every wrap. For a sound that repeats
    /// hundreds of times a minute that click becomes the loudest thing in the mod, so the
    /// wrap is compared against the largest step inside the buffer rather than against zero.
    /// </summary>
    private static void AssertLoopsSeamlessly(
        Action<bool, string> assert,
        string name,
        float[] samples)
    {
        assert(samples.Length > 2, $"{name} must generate a usable loop");

        var largestStep = 0f;
        for (var i = 1; i < samples.Length; i++)
        {
            var step = Math.Abs(samples[i] - samples[i - 1]);
            if (step > largestStep) largestStep = step;
        }

        var wrap = Math.Abs(samples[0] - samples[samples.Length - 1]);
        assert(
            wrap <= largestStep * 1.5f,
            $"{name} does not loop seamlessly: the jump at the wrap ({wrap:0.0000}) is larger " +
            $"than any step inside the loop ({largestStep:0.0000}), so it clicks on every " +
            "repeat");
    }

    /// <summary>
    /// Every cue must name a distinct file, because those names are what a player types to
    /// replace a sound. Two cues sharing a name would silently replace both.
    /// </summary>
    private static void AssertSoundFileNames(Action<bool, string> assert)
    {
        var names = new System.Collections.Generic.HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var cue in Cues.All)
        {
            var name = Cues.SoundFileName(cue);
            assert(
                !string.IsNullOrEmpty(name) && name != "unknown",
                $"cue {cue} must have a replacement file name");

            // The wall ring names its eight directions rather than the category, so its own
            // entry is expected to coincide with the north tone.
            if (cue == CueId.AmbientWall) continue;

            assert(
                names.Add(name),
                $"the sound file name {name} is used by more than one cue, so replacing one " +
                "would silently replace the other");
        }
    }

    /// <summary>
    /// The melee cue must not fire so early that dodging on it wears off before the hit.
    ///
    /// The arithmetic is duplicated here rather than called, because MeleeWarningSchedule
    /// needs Unity. What is being pinned is the property, not the implementation: with the
    /// game's real numbers, a player who reacts to the cue must still be invulnerable when
    /// the attack lands. This is the defect the timing change exists to fix, and it is
    /// invisible in play - you simply take a hit you thought you had dodged.
    /// </summary>
    /// <summary>
    /// The beacon's three fixed notes, and the sectors that choose between them.
    ///
    /// Pitch here answers "is it ahead or behind", which panning cannot express at all, so
    /// the sector boundaries are the whole feature. Ahead is north — up-screen, which is what
    /// W produces — because movement in this game is world-absolute and facing is derived
    /// from it rather than steering it.
    /// </summary>
    internal static void AssertBeaconBands(Action<bool, string> assert)
    {
        assert(
            BeaconTone.Band(0f, 1f) == BeaconBand.Front,
            "straight up-screen must be the ahead note");
        assert(
            BeaconTone.Band(0f, -1f) == BeaconBand.Behind,
            "straight down-screen must be the behind note");
        assert(
            BeaconTone.Band(1f, 0f) == BeaconBand.Side &&
            BeaconTone.Band(-1f, 0f) == BeaconBand.Side,
            "due left and right must share the side note, since panning already says which");

        // A diagonal sits on the boundary and must land in the sector it leans into rather
        // than flickering; the sectors are generous for the same reason.
        assert(
            BeaconTone.Band(0.4f, 1f) == BeaconBand.Front,
            "a target mostly ahead must read as ahead rather than as a side");
        assert(
            BeaconTone.Band(1f, 0.4f) == BeaconBand.Side,
            "a target mostly to the side must read as a side rather than as ahead");

        assert(
            BeaconTone.Band(0f, 0f) == BeaconBand.Side,
            "a target on top of the player has no bearing and must not pick a note from noise");

        // Ahead high, behind low, and far enough apart to be told apart at once.
        var ahead = BeaconTone.Frequency(BeaconBand.Front);
        var side = BeaconTone.Frequency(BeaconBand.Side);
        var behind = BeaconTone.Frequency(BeaconBand.Behind);
        assert(
            ahead > side && side > behind,
            "the beacon notes must rise from behind through side to ahead, matching the rule " +
            "that higher means further up-screen");
        assert(
            ahead / behind >= 2f,
            $"ahead and behind must be far enough apart to be unmistakable (ratio " +
            $"{ahead / behind:0.00})");

        // Equal length whatever the note, which is why they are separate samples rather than
        // one sample pitch-shifted: a longer ping would smear the distance channel.
        var lengths = new[]
        {
            BeaconTone.Generate(BeaconBand.Front).Length,
            BeaconTone.Generate(BeaconBand.Side).Length,
            BeaconTone.Generate(BeaconBand.Behind).Length,
        };
        assert(
            lengths[0] == lengths[1] && lengths[1] == lengths[2],
            "the three beacon notes must be the same length, or the ping rate stops meaning " +
            "distance at close range");

        foreach (BeaconBand band in Enum.GetValues(typeof(BeaconBand)))
        {
            var samples = BeaconTone.Generate(band);
            assert(
                Math.Abs(samples[0]) < 0.001f &&
                Math.Abs(samples[samples.Length - 1]) < 0.001f,
                $"the {band} beacon note must begin and end at silence so it cannot click");
        }
    }

    internal static void AssertMeleeTiming(Action<bool, string> assert)
    {
        // EnemyScuttleSwiper.SignPostAttackDuration and PlayerController.DodgeDuration.
        const float impact = 0.5f;
        const float dodgeCover = 0.3f;
        const float reaction = 0.18f;

        var cueAt = Delay(impact, dodgeCover, reaction);

        assert(
            cueAt > 0f,
            "with a wind-up longer than a dodge, the cue must wait rather than fire on the " +
            "enemy's commit; firing immediately is what made dodging on it always too early");

        var press = cueAt + reaction;
        assert(
            press <= impact && press + dodgeCover >= impact,
            $"reacting to the cue must leave the player invulnerable at impact: press at " +
            $"{press:0.000}, protected until {press + dodgeCover:0.000}, hit at {impact:0.000}");

        // The old behaviour, kept as the counter-example so the regression is explicit.
        assert(
            0f + reaction + dodgeCover < impact,
            "sanity: firing on commit really did leave the player exposed, which is why the " +
            "cue is delayed at all");

        // A telegraph shorter than the reaction time must still warn, immediately.
        assert(
            Delay(0.1f, dodgeCover, reaction) == 0f,
            "a telegraph too short to wait inside must fire at once; a late warning still " +
            "beats no warning");
        assert(
            Delay(0.5f, 0.3f, 0f) <= 0.35f,
            "with no reaction allowance the cue still aims the dodge at the impact");
    }

    private static float Delay(float impactIn, float dodgeCover, float reaction)
    {
        var cue = impactIn - dodgeCover * 0.5f - reaction;
        return cue < 0f ? 0f : cue > impactIn ? impactIn : cue;
    }

    private static float Rms(float[] samples) => Rms(samples, 0, samples.Length);

    private static float Rms(float[] samples, int from, int to)
    {
        double total = 0;
        for (var i = from; i < to; i++) total += samples[i] * samples[i];

        return to <= from ? 0f : (float)Math.Sqrt(total / (to - from));
    }

    /// <summary>
    /// No two cues may be near-copies of each other. This is the regression test for the
    /// defect that prompted the rework: the dodge cue was a 650 to 1350 Hz rise and the evade
    /// confirmation a 700 to 1450 Hz rise, and they were reported as indistinguishable.
    /// </summary>
    private static void AssertDistinct(Action<bool, string> assert)
    {
        for (var a = 0; a < Cues.All.Length; a++)
        for (var b = a + 1; b < Cues.All.Length; b++)
        {
            var first = Cues.All[a];
            var second = Cues.All[b];

            // The blocked dodge is the plain dodge with a knock appended, on purpose: they
            // describe the same action and one is the other plus bad news. They are separated
            // by length instead, which is checked below.
            if (IsDodgePair(first, second))
            {
                var plain = CueLibrary.Generate(CueId.DodgeDirection).Length;
                var blocked = CueLibrary.Generate(CueId.DodgeBlocked).Length;
                assert(
                    blocked > plain * 1.5,
                    "the blocked dodge must be clearly longer than the plain dodge, since " +
                    "they share their opening by design");
                continue;
            }

            var correlation = Correlation(
                CueLibrary.Generate(first), CueLibrary.Generate(second));
            assert(
                correlation < MaximumUnrelatedCorrelation,
                $"cues {first} and {second} are too alike to tell apart " +
                $"(correlation {correlation:0.000})");
        }
    }

    /// <summary>
    /// The specific pair that failed, checked on the property that failed. Correlation alone
    /// would pass on two sweeps an octave apart; what makes these two separable is that one
    /// is noise and the other is a tone, which the zero-crossing rate exposes directly.
    /// </summary>
    private static void AssertDodgeIsNotEvade(Action<bool, string> assert)
    {
        var dodge = ZeroCrossingRate(CueLibrary.Generate(CueId.DodgeDirection));
        var evaded = ZeroCrossingRate(CueLibrary.Generate(CueId.DodgeAvoidedHit));

        assert(
            dodge > evaded * 5f,
            "the dodge cue must be built from noise and the evade confirmation from tone, so " +
            $"they cannot be confused (dodge {dodge:0}, evade {evaded:0} crossings per second)");
    }

    /// <summary>
    /// The trap warning has to finish inside the trap's own wind-up, or it arrives with the
    /// damage rather than before it and warns about nothing.
    /// </summary>
    private static void AssertTrapFitsItsTelegraph(Action<bool, string> assert)
    {
        const float telegraphSeconds = 0.5f;

        var seconds = CueLibrary.Generate(CueId.StaticTrap).Length / (float)Waveform.SampleRate;
        assert(
            seconds < telegraphSeconds * 0.75f,
            $"the static trap cue must finish well inside the trap's {telegraphSeconds} second " +
            $"wind-up, leaving time to react (it lasts {seconds:0.000})");

        assert(
            CueLibrary.AudibilityGain(CueId.StaticTrap) >=
            CueLibrary.AudibilityGain(CueId.MeleeThreat),
            "the static trap cue must be at least as loud as the melee warning; there is no " +
            "second chance to notice it");
    }

    /// <summary>
    /// The always-on layer is only usable as an orientation aid if things stay quiet until
    /// they are close. These assert the shape of that curve rather than trusting it by eye.
    /// </summary>
    private static void AssertFalloff(Action<bool, string> assert)
    {
        const float radius = 5f;
        const float exponent = 2.2f;

        assert(
            Math.Abs(SoundscapeFalloff.Volume(0f, radius, exponent) - 1f) < 0.0001f,
            "an always-on source at zero distance must play at full volume");
        assert(
            SoundscapeFalloff.Volume(radius, radius, exponent) == 0f,
            "an always-on source at the configured range must be silent, so the range setting " +
            "means what it says");
        assert(
            SoundscapeFalloff.Volume(radius + 10f, radius, exponent) == 0f,
            "an always-on source beyond the range must stay silent");

        var previous = float.MaxValue;
        for (var distance = 0f; distance <= radius; distance += 0.25f)
        {
            var volume = SoundscapeFalloff.Volume(distance, radius, exponent);
            assert(volume <= previous, "always-on volume must never rise with distance");
            previous = volume;
        }

        // The requirement in the player's own words: a wall should be essentially inaudible
        // until a few metres away. At the default range and curve, halfway out must be quiet
        // enough to sit under the game rather than fill the room.
        assert(
            SoundscapeFalloff.Volume(radius * 0.5f, radius, exponent) < 0.25f,
            "at half its range an always-on source must already be well below half volume, or " +
            "the layer fills the room instead of describing what is close");

        var audible = SoundscapeFalloff.AudibleDistance(radius, exponent);
        assert(
            audible > 0f && audible < radius,
            "the audible distance must fall inside the configured range");
        assert(
            Math.Abs(SoundscapeFalloff.Volume(audible, radius, exponent) -
                     SoundscapeFalloff.AudibleFloor) < 0.005f,
            "the reported audible distance must be where the volume actually reaches the " +
            "floor, since that number is spoken to the player");
    }

    private static bool IsDodgePair(CueId first, CueId second) =>
        (first == CueId.DodgeDirection && second == CueId.DodgeBlocked) ||
        (first == CueId.DodgeBlocked && second == CueId.DodgeDirection);

    /// <summary>Normalised correlation over the overlapping part of two buffers.</summary>
    private static double Correlation(float[] first, float[] second)
    {
        var length = Math.Min(first.Length, second.Length);
        double dot = 0, firstEnergy = 0, secondEnergy = 0;

        for (var i = 0; i < length; i++)
        {
            dot += first[i] * second[i];
            firstEnergy += first[i] * first[i];
            secondEnergy += second[i] * second[i];
        }

        if (firstEnergy <= 0 || secondEnergy <= 0) return 0;

        return Math.Abs(dot) / Math.Sqrt(firstEnergy * secondEnergy);
    }

    /// <summary>Sign changes per second: high for noise, low for a tone.</summary>
    private static float ZeroCrossingRate(float[] samples)
    {
        if (samples.Length < 2) return 0f;

        var crossings = 0;
        for (var i = 1; i < samples.Length; i++)
            if ((samples[i - 1] < 0f) != (samples[i] < 0f))
                crossings++;

        return crossings * (float)Waveform.SampleRate / samples.Length;
    }
}
