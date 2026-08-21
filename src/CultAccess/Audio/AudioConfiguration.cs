using System.Collections.Generic;
using BepInEx.Configuration;
using CultAccess.Navigation;
using UnityEngine;

namespace CultAccess.Audio
{
    /// <summary>
    /// Binds every sound setting, keeping audio wiring out of the plugin entry point.
    ///
    /// One switch and one volume per cue, generated from <see cref="Cues.All"/> rather than
    /// written out by hand, so a cue added to that list is configurable the moment it exists
    /// and cannot be forgotten here.
    ///
    /// The entries are the single source of truth. The in-game menu writes to them, the
    /// config file writes to them, and both paths reach the runtime through the same
    /// <c>SettingChanged</c> handlers below — which is why a change made in game survives a
    /// restart without the menu knowing anything about persistence.
    /// </summary>
    internal static class AudioConfiguration
    {
        private const string CueSection = "Sound Cues";
        private const string AmbientSection = "Always-On Sounds";
        private const string MasterSection = "Sound";

        internal static readonly Dictionary<CueId, ConfigEntry<bool>> Enabled =
            new Dictionary<CueId, ConfigEntry<bool>>();
        internal static readonly Dictionary<CueId, ConfigEntry<float>> Volume =
            new Dictionary<CueId, ConfigEntry<float>>();
        internal static readonly Dictionary<CueGroup, ConfigEntry<bool>> GroupEnabled =
            new Dictionary<CueGroup, ConfigEntry<bool>>();
        internal static readonly Dictionary<CueId, ConfigEntry<float>> Radius =
            new Dictionary<CueId, ConfigEntry<float>>();
        internal static readonly Dictionary<CueId, ConfigEntry<float>> Interval =
            new Dictionary<CueId, ConfigEntry<float>>();
        internal static readonly Dictionary<CueId, ConfigEntry<int>> MaxSources =
            new Dictionary<CueId, ConfigEntry<int>>();

        internal static ConfigEntry<bool> MasterEnabled;
        internal static ConfigEntry<float> MasterVolume;
        internal static ConfigEntry<int> AmbientBudget;
        internal static ConfigEntry<WayfindingMode> GuidanceMode;
        internal static ConfigEntry<WallToneScheme> WallNotes;
        internal static ConfigEntry<WallRing> WallDirections;

        internal static void Bind(ConfigFile config)
        {
            Cues.Validate(message => Plugin.Log.LogError(message));
            Soundscape.Validate(message => Plugin.Log.LogError(message));

            MasterEnabled = config.Bind(MasterSection, "AllSounds", true,
                "Master switch for every generated sound the mod makes. Speech is unaffected.");
            MasterVolume = config.Bind(MasterSection, "OverallVolume", 1f,
                "Scales every generated sound, 0 to 1. Use this to balance the whole cue set " +
                "against speech and the game's own audio; individual cue volumes stay relative.");

            BindGroups(config);
            BindCues(config);
            BindAmbient(config);

            GuidanceMode = config.Bind("Wayfinding", "GuidanceMode",
                WayfindingMode.BeaconAndSpeech,
                "How walking guidance reaches you while it is running. BeaconAndSpeech uses " +
                "both. BeaconOnly stops the automatic spoken instructions and leaves the " +
                "beacon. SpeechOnly stops the beacon and leaves the instructions. Refusals, " +
                "arrivals, and the on-demand direction key always speak in every mode.");

            WallDirections = config.Bind(
                AmbientSection, "WallToneDirections", WallRing.FourDirections,
                "How many directions the wall tones probe. EightDirections includes the " +
                "diagonals. FourDirections is north, east, south and west only, which is " +
                "quieter and can make a doorway sharper: most walls are axis-aligned, so a " +
                "diagonal probe usually reports the same wall as its neighbour from further " +
                "away, and going quiet in three stages instead of one blurs the gap you are " +
                "listening for. The diagonals earn their place on angled walls and inside " +
                "corners. Defaults to four: it was the clearer of the two at a doorway, and " +
                "it halves both the raycasts and the number of tones that can sound at once.");

            WallNotes = config.Bind(AmbientSection, "WallToneNotes", WallToneScheme.AllNotes,
                "How much of a wall tone's direction is carried by its note rather than by " +
                "its position in the stereo field. AllNotes gives a note per screen height " +
                "and stays fully usable with no spatialisation. OneNote leaves direction " +
                "entirely to position. TwoNotes separates the sides from straight ahead and " +
                "behind. ThreeNotes spends pitch only on the axis panning cannot express: " +
                "high for up-screen, low for down-screen, and one note shared by both sides.");

            Apply();
            Subscribe();
        }

        /// <summary>Push every current value into the runtime. Also used after a reset.</summary>
        internal static void Apply()
        {
            CueSettings.MasterEnabled = MasterEnabled.Value;
            CueSettings.MasterVolume = MasterVolume.Value;
            Soundscape.MaxPingsPerSecond = AmbientBudget.Value;
            Wayfinding.Mode = GuidanceMode.Value;
            WallSonar.Scheme = WallNotes.Value;
            WallSonar.Ring = WallDirections.Value;

            foreach (var pair in GroupEnabled)
                CueSettings.SetGroupEnabled(pair.Key, pair.Value.Value);

            foreach (var cue in Cues.All)
            {
                CueSettings.SetEnabled(cue, Enabled[cue].Value);
                CueSettings.SetVolume(cue, Volume[cue].Value);

                if (!Cues.IsAmbient(cue)) continue;

                var settings = Soundscape.Settings(cue);
                settings.Radius = Radius[cue].Value;
                settings.Interval = Interval[cue].Value;
                settings.MaxSources = MaxSources[cue].Value;
            }
        }

        private static void BindGroups(ConfigFile config)
        {
            GroupEnabled[CueGroup.Wayfinding] = config.Bind(CueSection, "AllWayfindingCues", true,
                "Section switch for the navigation beacon.");
            GroupEnabled[CueGroup.Combat] = config.Bind(CueSection, "AllCombatCues", true,
                "Section switch for dodge, obstacle, threat, trap and evade cues. Turning it " +
                "off also stops the detection work behind them, so it costs nothing.");
            GroupEnabled[CueGroup.Minigames] = config.Bind(CueSection, "AllMinigameCues", true,
                "Section switch for minigame timing sounds, such as the cooking window.");
            GroupEnabled[CueGroup.Ambient] = config.Bind(CueSection, "AllAlwaysOnSounds", true,
                "Section switch for the always-on layer. Individual categories are off by " +
                "default and are turned on one at a time below.");
        }

        private static void BindCues(ConfigFile config)
        {
            foreach (var cue in Cues.All)
            {
                var key = Cues.ConfigKey(cue);
                Enabled[cue] = config.Bind(CueSection, key, DefaultEnabled(cue),
                    Describe(cue));
                Volume[cue] = config.Bind(CueSection, key + "Volume", DefaultVolume(cue),
                    $"Loudness of the {key} cue, 0 to 1, before the overall sound volume.");
            }
        }

        private static void BindAmbient(ConfigFile config)
        {
            AmbientBudget = config.Bind(AmbientSection, "MaxSoundsPerSecond", 40,
                "Hard ceiling on how many always-on sounds may play per second across every " +
                "category. Only reached in a crowded room; when it is, the categories listed " +
                "first keep sounding, so incoming fire survives and furniture does not.");

            foreach (var cue in Cues.All)
            {
                if (!Cues.IsAmbient(cue)) continue;

                var key = Cues.ConfigKey(cue);
                var defaults = SoundscapeSettings.Default(cue);

                Radius[cue] = config.Bind(AmbientSection, key + "Range", defaults.Radius,
                    "How far away this category can still be heard, in world units. Volume " +
                    "falls off steeply inside it, so things stay quiet until you are close; " +
                    "this is the distance at which they go completely silent.");
                Interval[cue] = config.Bind(AmbientSection, key + "Repeat", defaults.Interval,
                    "Seconds between repeats for one source in this category. Shorter is more " +
                    "present and more tiring.");
                MaxSources[cue] = config.Bind(AmbientSection, key + "MaxAtOnce", defaults.MaxSources,
                    "How many of this category may sound at once, nearest first. This is what " +
                    "stops a busy room becoming a wash.");
            }
        }

        private static void Subscribe()
        {
            MasterEnabled.SettingChanged +=
                (_, __) => CueSettings.MasterEnabled = MasterEnabled.Value;
            MasterVolume.SettingChanged +=
                (_, __) => CueSettings.MasterVolume = MasterVolume.Value;
            AmbientBudget.SettingChanged +=
                (_, __) => Soundscape.MaxPingsPerSecond = AmbientBudget.Value;
            GuidanceMode.SettingChanged += (_, __) => Wayfinding.Mode = GuidanceMode.Value;
            WallNotes.SettingChanged += (_, __) => WallSonar.Scheme = WallNotes.Value;
            WallDirections.SettingChanged += (_, __) => WallSonar.Ring = WallDirections.Value;

            foreach (var pair in GroupEnabled)
            {
                var group = pair.Key;
                var entry = pair.Value;
                entry.SettingChanged += (_, __) => CueSettings.SetGroupEnabled(group, entry.Value);
            }

            foreach (var cue in Cues.All)
            {
                var id = cue;

                var enabled = Enabled[id];
                enabled.SettingChanged += (_, __) => CueSettings.SetEnabled(id, enabled.Value);

                var volume = Volume[id];
                volume.SettingChanged += (_, __) => CueSettings.SetVolume(id, volume.Value);

                if (!Cues.IsAmbient(id)) continue;

                var radius = Radius[id];
                radius.SettingChanged += (_, __) => Soundscape.Settings(id).Radius = radius.Value;

                var interval = Interval[id];
                interval.SettingChanged +=
                    (_, __) => Soundscape.Settings(id).Interval = interval.Value;

                var maxSources = MaxSources[id];
                maxSources.SettingChanged +=
                    (_, __) => Soundscape.Settings(id).MaxSources = maxSources.Value;
            }
        }

        /// <summary>
        /// The always-on categories ship off. Everything that existed before this feature
        /// ships on, so an existing player hears no change until they ask for one.
        /// </summary>
        private static bool DefaultEnabled(CueId cue) => !Cues.IsAmbient(cue);

        private static float DefaultVolume(CueId cue)
        {
            switch (cue)
            {
                case CueId.Beacon:
                    return 0.5f;
                case CueId.TimingWindow:
                case CueId.TimingChirp:
                    return 0.55f;
                default:
                    return Cues.IsAmbient(cue) ? 0.45f : 0.55f;
            }
        }

        private static string Describe(CueId cue)
        {
            switch (cue)
            {
                case CueId.Beacon:
                    return "Repeating ping locating the tracked target. Stereo position is " +
                           "left and right, pitch is vertical aim rather than distance, and " +
                           "only the ping rate carries distance.";
                case CueId.WallNear:
                    return "Pulse when a wall or other solid object lies in the direction you " +
                           "are moving. Speeds up as it gets closer.";
                case CueId.WallBlocked:
                    return "Percussive knock when you make contact with solid geometry.";
                case CueId.DodgeDirection:
                    return "Marks the travel direction of a dodge made with no movement input, " +
                           "where the Lamb's facing decides where you go. A clearly directed " +
                           "dodge stays silent.";
                case CueId.DodgeBlocked:
                    return "Sounds when a dodge corridor contains solid geometry.";
                case CueId.MeleeThreat:
                    return "Descending warning at a supported enemy melee wind-up.";
                case CueId.ProjectileThreat:
                    return "High double warning for a hostile projectile predicted to hit you.";
                case CueId.AreaThreat:
                    return "Double warning at a grenade landing area you are standing in.";
                case CueId.StaticTrap:
                    return "Metallic rattle when a static hazard such as a spike trap has " +
                           "triggered and is about to deal damage. The loudest cue in the set.";
                case CueId.DodgeAvoidedHit:
                    return "Two rising notes confirming a dodge actually rejected an incoming hit.";
                case CueId.TimingWindow:
                    return "Sustained tone held for exactly as long as a minigame timing window " +
                           "is open. Press while you can hear it.";
                case CueId.TimingChirp:
                    return "Short chirp at the edge of a minigame timing window.";
                case CueId.AmbientWall:
                    return "Always-on orientation tones for the solid geometry around you, in " +
                           "eight directions. A gap in a wall is a direction that goes quiet.";
                case CueId.AmbientItem:
                    return "Always-on tick for dropped items and hearts near you.";
                case CueId.AmbientInteractable:
                    return "Always-on hum for interactable objects near you.";
                case CueId.AmbientNpc:
                    return "Always-on tone for followers and other characters near you.";
                case CueId.AmbientEnemy:
                    return "Always-on low tone for living hostiles near you.";
                case CueId.AmbientProjectile:
                    return "Always-on tick for hostile projectiles in the air, for bullet-hell " +
                           "patterns where individual warnings would arrive too late.";
                default:
                    return "Generated sound cue.";
            }
        }
    }
}
