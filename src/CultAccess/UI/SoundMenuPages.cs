using CultAccess.Audio;
using CultAccess.Localization;
using CultAccess.Speech;

namespace CultAccess.UI
{
    /// <summary>
    /// The sound half of the settings menu, and the learn-sounds walkthrough.
    ///
    /// Every page here is generated from <see cref="Cues.All"/>. That is the point: a cue
    /// cannot be added to the mod without appearing in the menu with its own switch, its own
    /// volume, and its own entry in the walkthrough, because there is nowhere else to list it.
    ///
    /// Each cue gets a small page of its own rather than a pair of rows in a long flat list.
    /// The flat version is faster to skim and much harder to trust: forty rows of alternating
    /// switches and volumes, read one at a time, gives no reliable sense of which volume
    /// belongs to which switch.
    /// </summary>
    internal static class SoundMenuPages
    {
        /// <summary>Range limits for the always-on categories, in world units.</summary>
        private const float MinimumRange = 2f;
        private const float MaximumRange = 30f;

        private const float MinimumRepeat = 0.1f;
        private const float MaximumRepeat = 3f;
        private const float RepeatStep = 0.05f;

        private const int MinimumSources = 1;
        private const int MaximumSources = 8;

        internal static ConfigPage Sounds()
        {
            var page = new ConfigPage(Strings.Get("config.sounds"));

            page.Add(ConfigItems.Toggle("config.all_sounds", AudioConfiguration.MasterEnabled));
            page.Add(ConfigItems.Percent("config.overall_volume", AudioConfiguration.MasterVolume));
            page.Add(ConfigItems.Submenu("config.combat_cues", GroupPage(CueGroup.Combat)));
            page.Add(ConfigItems.Submenu("config.always_on", AmbientPage()));
            page.Add(ConfigItems.Submenu("config.minigame_cues", GroupPage(CueGroup.Minigames)));
            page.Add(ConfigItems.Action(
                "config.sound_folder",
                () => Speaker.Say(Strings.Format("config.sound_folder_is", SoundBank.Folder))));

            return page;
        }

        /// <summary>
        /// The walkthrough. Moving onto a row plays the cue and says what it means, so
        /// learning the vocabulary is the same action as reading the list.
        /// </summary>
        internal static ConfigPage LearnSounds()
        {
            var page = new ConfigPage(Strings.Get("config.learn_sounds"));

            foreach (var cue in Cues.All)
            {
                var id = cue;
                page.Add(new ConfigItem
                {
                    Label = Strings.Get(Cues.Key(id)),
                    // The walkthrough is also where a player finds out what to call a
                    // replacement file, so the name is part of the explanation rather than
                    // buried in a readme they would have to leave the game to read.
                    Description = Strings.Get(Cues.Key(id) + ".detail") + " " +
                                  Strings.Format("config.sound_file_name", FileNames(id)),
                    Kind = ConfigItemKind.Action,
                    OnFocus = () => CuePreview.Play(id),
                    // Activating a row it already played on focus repeats it, and repeats it
                    // as a sweep: the second hearing is the one where the player is listening
                    // for where it is rather than what it is.
                    Activate = () => CuePreview.PlaySweep(id),
                });
            }

            return page;
        }

        /// <summary>A page listing every cue in one section, with the section switch on top.</summary>
        internal static ConfigPage GroupPage(CueGroup group)
        {
            var page = new ConfigPage(Strings.Get(GroupTitleKey(group)));

            page.Add(ConfigItems.Toggle(
                GroupToggleKey(group), AudioConfiguration.GroupEnabled[group]));

            foreach (var cue in Cues.All)
                if (Cues.Group(cue) == group)
                    page.Add(CueRow(cue));

            return page;
        }

        private static ConfigPage AmbientPage()
        {
            var page = GroupPage(CueGroup.Ambient);

            page.Add(ConfigItems.Slider(
                "config.ambient_budget",
                () => AudioConfiguration.AmbientBudget.Value,
                value => AudioConfiguration.AmbientBudget.Value = (int)value,
                5f, 80f, 5f,
                value => Strings.Format("config.per_second", (int)value)));

            return page;
        }

        /// <summary>
        /// What a replacement file for this cue is called. The wall ring has one file per
        /// direction rather than one for the category, so it names the whole set.
        /// </summary>
        private static string FileNames(CueId cue)
        {
            if (cue != CueId.AmbientWall) return Cues.SoundFileName(cue);

            var names = new string[WallDirection.Ring.Length];
            for (var i = 0; i < names.Length; i++)
                names[i] = WallDirection.Ring[i].SoundKey;

            return string.Join(", ", names);
        }

        private static ConfigItem CueRow(CueId cue) =>
            ConfigItems.Submenu(
                Strings.Get(Cues.Key(cue)),
                Strings.Get(Cues.Key(cue) + ".detail"),
                CuePage(cue));

        private static ConfigPage CuePage(CueId cue)
        {
            var id = cue;
            var page = new ConfigPage(Strings.Get(Cues.Key(id)));

            page.Add(WithSuppressionNotice(id,
                ConfigItems.Toggle("config.cue_on", AudioConfiguration.Enabled[id])));
            page.Add(WithSuppressionNotice(id,
                ConfigItems.Percent("config.cue_volume", AudioConfiguration.Volume[id])));

            if (Cues.IsAmbient(id))
            {
                page.Add(ConfigItems.Metres(
                    "config.cue_range",
                    () => AudioConfiguration.Radius[id].Value,
                    value => AudioConfiguration.Radius[id].Value = value,
                    MinimumRange, MaximumRange));

                // The wall ring sustains rather than repeating, so its interval is how often
                // the geometry is re-measured, not how often a sound plays. Same setting,
                // genuinely different meaning, so it gets its own label.
                var sustained = id == CueId.AmbientWall;
                if (sustained)
                    page.Add(ConfigItems.Choice(
                        "config.wall_ring",
                        () => (int)AudioConfiguration.WallDirections.Value,
                        value => AudioConfiguration.WallDirections.Value = (WallRing)value,
                        "config.wall_ring_four",
                        "config.wall_ring_eight"));

                if (sustained)
                    page.Add(ConfigItems.Choice(
                        "config.wall_notes",
                        () => (int)AudioConfiguration.WallNotes.Value,
                        value => AudioConfiguration.WallNotes.Value = (WallToneScheme)value,
                        "config.wall_notes_all",
                        "config.wall_notes_one",
                        "config.wall_notes_two",
                        "config.wall_notes_hrtf"));

                page.Add(ConfigItems.Seconds(
                    sustained ? "config.cue_update" : "config.cue_repeat",
                    () => AudioConfiguration.Interval[id].Value,
                    value => AudioConfiguration.Interval[id].Value = value,
                    MinimumRepeat, MaximumRepeat, RepeatStep));

                // Deliberately absent for the wall ring. Everywhere else a cap on how many
                // may sound at once is a courtesy; there it would be a lie, because a
                // direction culled to save noise is silent, and silent is exactly how the
                // player is being told they can walk that way. A cap would invent doorways.
                if (!sustained)
                    page.Add(ConfigItems.Slider(
                        "config.cue_max_at_once",
                        () => AudioConfiguration.MaxSources[id].Value,
                        value => AudioConfiguration.MaxSources[id].Value = (int)value,
                        MinimumSources, MaximumSources, 1f,
                        value => ((int)value).ToString()));
            }

            page.Add(ConfigItems.Action("config.play_it", () => CuePreview.Play(id)));

            // The wall ring demonstrates its own directions when played, so a separate sweep
            // would repeat the same lesson under a second name.
            if (id != CueId.AmbientWall)
                page.Add(ConfigItems.Action("config.hear_it_move", () => CuePreview.PlaySweep(id)));

            return page;
        }

        /// <summary>
        /// Mark a row as currently inaudible when a switch above it is off. Without this a
        /// player can turn a cue on, hear nothing, and have no way to discover that the
        /// section switch or the master switch is what is actually stopping it.
        /// </summary>
        private static ConfigItem WithSuppressionNotice(CueId cue, ConfigItem item)
        {
            item.Suppressed = () =>
                CueSettings.EnabledSetting(cue) && !CueSettings.Enabled(cue);
            return item;
        }

        private static string GroupTitleKey(CueGroup group)
        {
            switch (group)
            {
                case CueGroup.Wayfinding: return "config.wayfinding_cues";
                case CueGroup.Minigames: return "config.minigame_cues";
                case CueGroup.Ambient: return "config.always_on";
                default: return "config.combat_cues";
            }
        }

        private static string GroupToggleKey(CueGroup group)
        {
            switch (group)
            {
                case CueGroup.Wayfinding: return "config.all_wayfinding_cues";
                case CueGroup.Minigames: return "config.all_minigame_cues";
                case CueGroup.Ambient: return "config.all_always_on";
                default: return "config.all_combat_cues";
            }
        }
    }
}
