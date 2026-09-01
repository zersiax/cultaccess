using CultAccess.Audio;
using CultAccess.Combat;
using CultAccess.Localization;
using CultAccess.Navigation;

namespace CultAccess.UI
{
    /// <summary>
    /// Assembles the settings menu from the live config entries.
    ///
    /// Built lazily on first open, and rebuilt if the game's language changes, because the
    /// mod adopts the game's language a moment after startup rather than at load — building
    /// the tree in <c>Awake</c> would freeze every label in the startup default.
    /// </summary>
    internal static class ConfigMenuTree
    {
        private const float MinimumAnnounceInterval = 1f;
        private const float MaximumAnnounceInterval = 15f;
        private const float MinimumScanRadius = 10f;
        private const float MaximumScanRadius = 80f;
        private const float MinimumEnemyRange = 5f;
        private const float MaximumEnemyRange = 60f;
        private const float MinimumCookingLead = 0f;
        private const float MaximumCookingLead = 0.6f;

        private static ConfigPage _root;
        private static string _builtForLanguage;

        internal static ConfigPage Root()
        {
            if (_root != null && _builtForLanguage == Strings.Language) return _root;

            _builtForLanguage = Strings.Language;
            _root = Build();
            return _root;
        }

        /// <summary>Drop the cached tree, so the next open rebuilds it.</summary>
        internal static void Invalidate()
        {
            _root = null;
            _builtForLanguage = null;
        }

        private static ConfigPage Build()
        {
            var root = new ConfigPage(Strings.Get("config.title"));

            root.Add(ConfigItems.Submenu("config.speech", SpeechPage()));
            root.Add(ConfigItems.Submenu("config.sounds", SoundMenuPages.Sounds()));
            root.Add(ConfigItems.Submenu("config.wayfinding", WayfindingPage()));
            root.Add(ConfigItems.Submenu("config.learn_sounds", SoundMenuPages.LearnSounds()));
            root.Add(ConfigItems.Submenu("config.diagnostics", DiagnosticsPage()));

            return root;
        }

        private static ConfigPage SpeechPage()
        {
            var page = new ConfigPage(Strings.Get("config.speech"));

            page.Add(ConfigItems.Toggle("config.speech_enabled", Plugin.SpeechEnabled));
            page.Add(ConfigItems.Choice(
                "config.verbosity",
                () => (int)Plugin.Verbosity.Value,
                value => Plugin.Verbosity.Value = (Verbosity)value,
                "config.verbosity_low", "config.verbosity_normal", "config.verbosity_diagnostic"));
            page.Add(ConfigItems.Toggle("config.braille", Plugin.BrailleEnabled));
            page.Add(ConfigItems.Toggle("config.menu_context", Plugin.AnnounceMenuContext));
            page.Add(ConfigItems.Toggle("config.auto_read_panels", Plugin.AutoReadPanels));
            page.Add(ConfigItems.Toggle("config.interaction_prompts",
                Plugin.AnnounceInteractionPrompts));
            page.Add(ConfigItems.Toggle("config.notifications", Plugin.AnnounceNotifications));
            page.Add(ConfigItems.Toggle("config.barks", Plugin.AnnounceBarks));
            page.Add(ConfigItems.Toggle("config.player_state", Plugin.AnnouncePlayerStateChanges));
            page.Add(ConfigItems.Toggle("config.day_cycle", Plugin.AnnounceDayCycle));
            page.Add(ConfigItems.Toggle("config.chore_progress", Plugin.AnnounceChoreProgress));
            page.Add(ConfigItems.Toggle("config.onboarding", Plugin.AnnounceOnboardingPhase));
            page.Add(ConfigItems.Toggle("config.cult_status", Plugin.AnnounceCultStatus));
            page.Add(ConfigItems.Toggle(
                "config.cult_in_where_am_i", Plugin.AnnounceCultInWhereAmI));
            page.Add(ConfigItems.Toggle(
                "config.follower_requests", Plugin.AnnounceFollowerRequests));
            page.Add(ConfigItems.Toggle("config.combat_state",
                CombatConfiguration.AnnounceCombatState));

            return page;
        }

        private static ConfigPage WayfindingPage()
        {
            var page = new ConfigPage(Strings.Get("config.wayfinding"));

            page.Add(ConfigItems.Choice(
                "config.guidance_mode",
                () => (int)AudioConfiguration.GuidanceMode.Value,
                value => AudioConfiguration.GuidanceMode.Value = (WayfindingMode)value,
                "config.mode_beacon_and_speech",
                "config.mode_beacon_only",
                "config.mode_speech_only"));

            page.Add(ConfigItems.Toggle("config.autowalk", Plugin.AutowalkAvailable));

            page.Add(ConfigItems.Submenu("config.beacon_cues",
                SoundMenuPages.GroupPage(CueGroup.Wayfinding)));

            page.Add(ConfigItems.Seconds(
                "config.announce_interval",
                () => Plugin.AutoAnnounceInterval.Value,
                value => Plugin.AutoAnnounceInterval.Value = value,
                MinimumAnnounceInterval, MaximumAnnounceInterval, 0.5f));

            page.Add(ConfigItems.Metres(
                "config.scan_radius",
                () => Plugin.ScanRadius.Value,
                value => Plugin.ScanRadius.Value = value,
                MinimumScanRadius, MaximumScanRadius));

            page.Add(ConfigItems.Metres(
                "config.enemy_range",
                () => Plugin.EnemyRange.Value,
                value => Plugin.EnemyRange.Value = value,
                MinimumEnemyRange, MaximumEnemyRange));

            page.Add(ConfigItems.Seconds(
                "config.projectile_horizon",
                () => CombatConfiguration.WarningHorizon.Value,
                value => CombatConfiguration.WarningHorizon.Value = value,
                0.2f, 3f, 0.1f));

            page.Add(ConfigItems.Seconds(
                "config.melee_reaction",
                () => CombatConfiguration.MeleeReaction.Value,
                value => CombatConfiguration.MeleeReaction.Value = value,
                0f, 0.5f, 0.02f));


            page.Add(ConfigItems.Seconds(
                "config.cooking_lead",
                () => Plugin.CookingCueLead.Value,
                value => Plugin.CookingCueLead.Value = value,
                MinimumCookingLead, MaximumCookingLead, 0.02f));

            return page;
        }

        private static ConfigPage DiagnosticsPage()
        {
            var page = new ConfigPage(Strings.Get("config.diagnostics"));

            page.Add(ConfigItems.Toggle("config.log_navigation", Plugin.LogNavigation));
            page.Add(ConfigItems.Toggle("config.log_combat", CombatConfiguration.LogCombat));
            page.Add(ConfigItems.Toggle("config.log_dialogue", Plugin.LogDialogue));
            page.Add(ConfigItems.Toggle("config.log_scan", Plugin.LogScanCandidates));
            page.Add(ConfigItems.Toggle("config.log_bindings", Plugin.LogGameBindings));
            page.Add(ConfigItems.Toggle("config.log_frame_budget", Plugin.LogFrameBudget));
            page.Add(ConfigItems.Toggle("config.log_follower_wheel", Plugin.LogFollowerWheel));

            return page;
        }
    }
}
