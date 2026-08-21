using System;

namespace CultAccess.Audio
{
    /// <summary>
    /// Sample synthesis for the mod's generated earcons.
    ///
    /// Deliberately free of any Unity or FMOD dependency so the pure test harness can assert
    /// the properties that matter about a cue and that are impossible to check by ear during
    /// a play session: that it never clips, that it starts and ends at silence so it cannot
    /// click, and that two cues meant to be distinguishable really do differ.
    ///
    /// The vocabulary separates cues along three independent axes, because pitch alone is not
    /// enough under combat audio:
    ///
    /// - **Timbre** — a pure sine, a harmonic stack, an inharmonic (bell-like) stack, or
    ///   filtered noise. Noise and inharmonic partials are reserved for the two cues that
    ///   must never be mistaken for anything else.
    /// - **Rhythm** — one event, two, or an accelerating burst.
    /// - **Register** — where in the spectrum it sits.
    ///
    /// The first cue set used glides almost exclusively and two of them landed within a few
    /// percent of each other, which is exactly the failure this structure exists to prevent.
    /// </summary>
    internal static class Waveform
    {
        internal const int SampleRate = 44100;

        /// <summary>
        /// A fixed seed, so a cue generated on two machines is byte-identical and a report
        /// that "the sound changed" is answerable.
        /// </summary>
        private const int NoiseSeed = 20260819;

        internal static int SampleCount(float seconds) =>
            Math.Max(1, (int)(SampleRate * seconds));

        /// <summary>Raised cosine: silent at both ends, so no cue can click.</summary>
        internal static float Bell(float progress) =>
            0.5f * (1f - (float)Math.Cos(Clamp01(progress) * 2f * Math.PI));

        /// <summary>Fast attack then squared decay, for anything percussive.</summary>
        internal static float Percussive(float progress, float attackFraction)
        {
            progress = Clamp01(progress);
            var attack = attackFraction <= 0f ? 1f : Clamp01(progress / attackFraction);
            var decay = 1f - progress;
            return attack * decay * decay;
        }

        /// <summary>
        /// A frequency glide with a harmonic stack. <paramref name="harmonics"/> holds the
        /// amplitude of each partial starting at the fundamental.
        ///
        /// <paramref name="pulses"/> above one re-triggers the envelope that many times
        /// across the same sweep, separated by a short silence. That is how the two-pulse
        /// threat warnings are built, and it is deliberately the same arithmetic the first
        /// cue set used, so cues the player has already learned are unchanged.
        /// </summary>
        internal static float[] Glide(
            float seconds,
            float startFrequency,
            float endFrequency,
            float amplitude,
            float[] harmonics = null,
            int pulses = 1)
        {
            const float DutyCycle = 0.78f;

            var count = SampleCount(seconds);
            var samples = new float[count];
            var phase = 0.0;
            if (pulses < 1) pulses = 1;

            for (var i = 0; i < count; i++)
            {
                var progress = (float)i / count;
                var frequency = Lerp(startFrequency, endFrequency, progress);
                phase += 2.0 * Math.PI * frequency / SampleRate;

                var localProgress = progress;
                var gate = 1f;
                if (pulses > 1)
                {
                    localProgress = (progress * pulses) % 1f;
                    gate = localProgress < DutyCycle ? 1f : 0f;
                    localProgress = Clamp01(localProgress / DutyCycle);
                }

                samples[i] = Clamp(
                    Stack(phase, harmonics) * Bell(localProgress) * gate * amplitude);
            }

            return samples;
        }

        /// <summary>
        /// Discrete pitches played in sequence, each with its own bell envelope.
        ///
        /// Rhythmically distinct from <see cref="Glide"/> even at the same frequencies: a
        /// two-note step and a continuous sweep are told apart instantly, where two sweeps a
        /// few percent apart are not.
        /// </summary>
        internal static float[] Steps(
            float noteSeconds,
            float[] frequencies,
            float amplitude,
            float gapSeconds = 0f,
            float[] harmonics = null)
        {
            if (frequencies == null || frequencies.Length == 0)
                return new float[SampleCount(noteSeconds)];

            var noteCount = SampleCount(noteSeconds);
            var gapCount = gapSeconds <= 0f ? 0 : SampleCount(gapSeconds);
            var samples = new float[(noteCount + gapCount) * frequencies.Length];

            for (var note = 0; note < frequencies.Length; note++)
            {
                var offset = note * (noteCount + gapCount);
                var phase = 0.0;

                for (var i = 0; i < noteCount; i++)
                {
                    phase += 2.0 * Math.PI * frequencies[note] / SampleRate;
                    samples[offset + i] =
                        Clamp(Stack(phase, harmonics) * Bell((float)i / noteCount) * amplitude);
                }
            }

            return samples;
        }

        /// <summary>
        /// A broad, fast-decaying knock built from inharmonic partials. Reads as an impact
        /// rather than a note, which is what a collision should sound like.
        /// </summary>
        internal static float[] Impact(
            float seconds,
            float startFrequency,
            float endFrequency,
            float amplitude)
        {
            var count = SampleCount(seconds);
            var samples = new float[count];
            var phase = 0.0;

            for (var i = 0; i < count; i++)
            {
                var progress = (float)i / count;
                var frequency = Lerp(startFrequency, endFrequency, progress);
                phase += 2.0 * Math.PI * frequency / SampleRate;
                var body = (float)(Math.Sin(phase) +
                                   Math.Sin(phase * 2.37) * 0.42 +
                                   Math.Sin(phase * 6.91) * 0.18);
                samples[i] = Clamp(body * Percussive(progress, 0.035f) * amplitude);
            }

            return samples;
        }

        /// <summary>
        /// A harsh, buzzing descent: a dense harmonic stack with a hard attack and a fast
        /// tremolo over the top.
        ///
        /// Reserved for the melee wind-up. The first version of that cue was a smooth sine
        /// glide, and it was reported as far too quiet and unassuming for what it means —
        /// correctly, because a smooth sine is the most polite sound available and this is a
        /// warning that something is about to hit you. Loudness alone would not have fixed it:
        /// a pure tone at any level still reads as an announcement rather than a threat.
        ///
        /// A saw-like stack has energy spread across the spectrum, so it cuts through the
        /// game's own mix at a far lower peak level than a sine needs, and the tremolo makes
        /// it snarl. It is also the only cue built this way, which keeps it distinct.
        /// </summary>
        internal static float[] Growl(
            float seconds,
            float startFrequency,
            float endFrequency,
            float amplitude,
            float tremoloHz,
            float tremoloDepth)
        {
            const float AttackFraction = 0.012f;
            const float ReleaseStart = 0.72f;

            var count = SampleCount(seconds);
            var samples = new float[count];
            var phase = 0.0;

            for (var i = 0; i < count; i++)
            {
                var progress = (float)i / count;
                var frequency = Lerp(startFrequency, endFrequency, progress);
                phase += 2.0 * Math.PI * frequency / SampleRate;

                // Hold, then release. A percussive decay would soften exactly the part that
                // has to stay urgent, so the level is held up and then cut.
                var envelope = progress < AttackFraction
                    ? progress / AttackFraction
                    : progress < ReleaseStart
                        ? 1f
                        : 1f - (progress - ReleaseStart) / (1f - ReleaseStart);

                var tremolo = 1f - tremoloDepth *
                    (0.5f - 0.5f * (float)Math.Cos(2.0 * Math.PI * tremoloHz * i / SampleRate));

                samples[i] = Clamp(Stack(phase, SawHarmonics) * envelope * tremolo * amplitude);
            }

            return samples;
        }

        /// <summary>Roughly 1/n, the shape of a sawtooth: bright, dense and aggressive.</summary>
        private static readonly float[] SawHarmonics =
            { 1f, 0.62f, 0.44f, 0.33f, 0.26f, 0.2f, 0.16f };

        /// <summary>
        /// A burst of sharp inharmonic strikes that accelerate.
        ///
        /// Reserved for the static-trap warning. Nothing else in the vocabulary rattles, and
        /// an accelerating rhythm is heard as "closing in" without having to be taught.
        /// </summary>
        internal static float[] Rattle(
            float seconds,
            int strikes,
            float frequency,
            float amplitude)
        {
            var count = SampleCount(seconds);
            var samples = new float[count];
            if (strikes < 1) strikes = 1;

            var strikeLength = SampleCount(0.045f);

            for (var strike = 0; strike < strikes; strike++)
            {
                // Strike positions bunch toward the end, so the burst tightens as it runs.
                var at = (float)Math.Pow((double)strike / strikes, 1.6);
                var start = (int)(at * count);
                var length = Math.Min(count - start, strikeLength);
                if (length <= 0) continue;

                var strikeFrequency = frequency * (1f + 0.12f * strike);
                var phase = 0.0;

                for (var i = 0; i < length; i++)
                {
                    phase += 2.0 * Math.PI * strikeFrequency / SampleRate;
                    // Bell-like inharmonic ratios: unmistakably metallic, never a note.
                    var body = (float)(Math.Sin(phase) +
                                       Math.Sin(phase * 2.756) * 0.55 +
                                       Math.Sin(phase * 5.404) * 0.33 +
                                       Math.Sin(phase * 8.933) * 0.16);
                    var envelope = Percussive((float)i / length, 0.004f);
                    samples[start + i] = Clamp(samples[start + i] + body * envelope * amplitude);
                }
            }

            return samples;
        }

        /// <summary>
        /// Filtered noise with a percussive envelope — a transient with no pitch at all.
        ///
        /// The only cue material in the mod that carries no tone, which is what makes the
        /// dodge cue impossible to confuse with the tonal warnings around it. A one-pole pair
        /// gives a broad band; precision is not the point, distinctiveness is.
        /// </summary>
        internal static float[] NoiseBurst(
            float seconds,
            float lowCutoff,
            float highCutoff,
            float amplitude,
            float attackFraction = 0.01f)
        {
            var count = SampleCount(seconds);
            var samples = new float[count];
            var random = new Random(NoiseSeed);

            var lowPassCoefficient = Coefficient(highCutoff);
            var highPassCoefficient = Coefficient(lowCutoff);
            var lowPass = 0f;
            var highPass = 0f;

            for (var i = 0; i < count; i++)
            {
                var white = (float)(random.NextDouble() * 2.0 - 1.0);
                lowPass += lowPassCoefficient * (white - lowPass);
                highPass += highPassCoefficient * (lowPass - highPass);
                var band = lowPass - highPass;
                samples[i] = Clamp(band * Percussive((float)i / count, attackFraction) *
                                   amplitude * 2.6f);
            }

            return samples;
        }

        /// <summary>
        /// A seamlessly loopable steady tone.
        ///
        /// The buffer holds a whole number of periods exactly, so the loop point always lands
        /// on the same phase and the wrap is inaudible. Getting this wrong is not subtle: a
        /// partial period clicks on every wrap, which for a tone that repeats several times a
        /// second becomes the loudest thing in the mix.
        ///
        /// The count is derived rather than fixed because the period length in samples is not
        /// generally an integer; rounding the buffer to the nearest whole sample and then
        /// deriving the phase from the buffer length keeps both ends consistent.
        /// </summary>
        internal static float[] Loop(
            float frequency,
            int cycles,
            float amplitude,
            float[] harmonics = null)
        {
            if (cycles < 1) cycles = 1;
            if (frequency <= 0f) frequency = 1f;

            var count = Math.Max(2, (int)Math.Round(cycles * (double)SampleRate / frequency));
            var samples = new float[count];

            for (var i = 0; i < count; i++)
            {
                var phase = 2.0 * Math.PI * cycles * i / count;
                samples[i] = Clamp(Stack(phase, harmonics) * amplitude);
            }

            return samples;
        }

        /// <summary>Lay one buffer over another at an offset, summing and clamping.</summary>
        internal static float[] Layer(float[] first, float[] second, float offsetSeconds)
        {
            if (first == null) return second ?? new float[0];
            if (second == null) return first;

            var offset = offsetSeconds <= 0f ? 0 : SampleCount(offsetSeconds);
            var samples = new float[Math.Max(first.Length, offset + second.Length)];
            Array.Copy(first, samples, first.Length);

            for (var i = 0; i < second.Length; i++)
                samples[offset + i] = Clamp(samples[offset + i] + second[i]);

            return samples;
        }

        /// <summary>Join buffers end to end with an optional silence between them.</summary>
        internal static float[] Then(float[] first, float[] second, float gapSeconds)
        {
            if (first == null) return second ?? new float[0];
            if (second == null) return first;

            var gap = gapSeconds <= 0f ? 0 : SampleCount(gapSeconds);
            var samples = new float[first.Length + gap + second.Length];
            Array.Copy(first, 0, samples, 0, first.Length);
            Array.Copy(second, 0, samples, first.Length + gap, second.Length);
            return samples;
        }

        private static float Stack(double phase, float[] harmonics)
        {
            if (harmonics == null || harmonics.Length == 0)
                return (float)Math.Sin(phase);

            var value = 0f;
            for (var h = 0; h < harmonics.Length; h++)
            {
                if (harmonics[h] == 0f) continue;
                value += (float)Math.Sin(phase * (h + 1)) * harmonics[h];
            }

            return value;
        }

        /// <summary>One-pole coefficient for a cutoff in Hz, clamped to a stable range.</summary>
        private static float Coefficient(float cutoffHz)
        {
            var normalised = Clamp01(cutoffHz / (SampleRate * 0.5f));
            return Math.Max(0.0005f, Math.Min(0.999f, normalised * 1.6f));
        }

        private static float Lerp(float from, float to, float t) => from + (to - from) * t;

        private static float Clamp(float value) => value < -1f ? -1f : value > 1f ? 1f : value;

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
