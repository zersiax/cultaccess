using System;

namespace CultAccess.Audio
{
    /// <summary>
    /// The player's on/off and volume choice for every cue, plus the master switch.
    ///
    /// Held as plain arrays rather than read from the config file on each play, because cues
    /// fire inside the update loop and several can fire in the same frame. The configuration
    /// menu and the config file both write here, and both persist through the BepInEx entries
    /// that own these values, so a change made in game survives a restart.
    /// </summary>
    internal static class CueSettings
    {
        private static readonly bool[] EnabledByCue;
        private static readonly float[] VolumeByCue;
        private static readonly bool[] EnabledByGroup;

        /// <summary>Turns off every generated sound at once, leaving speech alone.</summary>
        internal static bool MasterEnabled = true;

        /// <summary>Scales every cue, so the whole soundscape can be balanced against speech.</summary>
        internal static float MasterVolume = 1f;

        static CueSettings()
        {
            // Sized from the enum rather than from Cues.All, so a cue accidentally left out
            // of that list still gets a slot and a sane default instead of falling off the
            // end of the array. Cues.Validate is what reports the omission itself.
            var count = 0;
            foreach (CueId cue in Enum.GetValues(typeof(CueId)))
                if ((int)cue + 1 > count) count = (int)cue + 1;

            EnabledByCue = new bool[count];
            VolumeByCue = new float[count];
            EnabledByGroup = new bool[4];

            for (var i = 0; i < count; i++)
            {
                EnabledByCue[i] = true;
                VolumeByCue[i] = 0.55f;
            }

            for (var i = 0; i < EnabledByGroup.Length; i++)
                EnabledByGroup[i] = true;

            // The always-on categories start silent. They are opt-in by design: the mod must
            // sound exactly as it did before this feature until the player chooses otherwise.
            foreach (CueId cue in Enum.GetValues(typeof(CueId)))
                if (Cues.IsAmbient(cue))
                    EnabledByCue[(int)cue] = false;
        }

        internal static bool Enabled(CueId cue) =>
            MasterEnabled && GroupEnabled(Cues.Group(cue)) &&
            InRange(cue) && EnabledByCue[(int)cue];

        /// <summary>
        /// The section master, so a player can silence all combat cues or the whole ambient
        /// soundscape without losing the individual choices underneath.
        /// </summary>
        internal static bool GroupEnabled(CueGroup group) =>
            (int)group < 0 || (int)group >= EnabledByGroup.Length || EnabledByGroup[(int)group];

        internal static void SetGroupEnabled(CueGroup group, bool enabled)
        {
            if ((int)group >= 0 && (int)group < EnabledByGroup.Length)
                EnabledByGroup[(int)group] = enabled;
        }

        /// <summary>The player's own setting, ignoring the master switch, for the menu.</summary>
        internal static bool EnabledSetting(CueId cue) => InRange(cue) && EnabledByCue[(int)cue];

        internal static void SetEnabled(CueId cue, bool enabled)
        {
            if (InRange(cue)) EnabledByCue[(int)cue] = enabled;
        }

        internal static float Volume(CueId cue) =>
            InRange(cue) ? Clamp01(VolumeByCue[(int)cue]) : 0f;

        internal static void SetVolume(CueId cue, float volume)
        {
            if (InRange(cue)) VolumeByCue[(int)cue] = Clamp01(volume);
        }

        /// <summary>
        /// The channel volume for one play: the player's cue volume, the master volume, the
        /// per-cue perceptual trim, and any per-event scaling such as distance falloff.
        /// </summary>
        internal static float ChannelVolume(CueId cue, float scale) =>
            Clamp01(Volume(cue) * Clamp01(MasterVolume) * CueLibrary.AudibilityGain(cue) *
                    Math.Max(0f, scale));

        private static bool InRange(CueId cue) =>
            (int)cue >= 0 && (int)cue < EnabledByCue.Length;

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
