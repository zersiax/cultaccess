using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CultAccess.Input;
using CultAccess.Speech;
using HarmonyLib;
using UnityEngine;

namespace CultAccess
{
    [BepInPlugin(Guid, "CultAccess", "0.2.2")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dev.cultaccess";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        /// <summary>Directory the plugin DLL lives in; native speech libraries go here.</summary>
        internal static string PluginDir;

        internal static ConfigEntry<bool> SpeechEnabled;
        internal static ConfigEntry<UI.Verbosity> Verbosity;
        internal static ConfigEntry<bool> AnnounceMenuContext;
        internal static ConfigEntry<bool> BrailleEnabled;
        internal static ConfigEntry<bool> AnnounceBarks;
        internal static ConfigEntry<bool> AnnounceNotifications;
        internal static ConfigEntry<bool> AnnounceInteractionPrompts;
        internal static ConfigEntry<bool> AnnouncePlayerStateChanges;
        internal static ConfigEntry<bool> AnnounceOnboardingPhase;
        internal static ConfigEntry<bool> AnnounceDayCycle;
        internal static ConfigEntry<bool> AnnounceChoreProgress;
        internal static ConfigEntry<bool> AnnounceReeducation;
        internal static ConfigEntry<bool> AnnounceHelpHinder;
        internal static ConfigEntry<bool> AnnounceSermonResult;
        internal static ConfigEntry<float> CookingCueLead;
        internal static ConfigEntry<KeyCode> KeyHelp;
        internal static ConfigEntry<KeyCode> KeyConfigMenu;
        internal static ConfigEntry<bool> LogDialogue;
        internal static ConfigEntry<bool> LogFocus;
        internal static ConfigEntry<bool> LogSpeech;
        internal static ConfigEntry<bool> LogCueAudio;
        internal static ConfigEntry<bool> LogCurseAim;
        internal static ConfigEntry<bool> LogSilentSequences;
        internal static ConfigEntry<bool> AnnounceMapReveals;
        internal static ConfigEntry<bool> AnnounceChainBreaks;
        internal static ConfigEntry<bool> WarnOnNonStereoOutput;
        internal static ConfigEntry<bool> ControllerLayerEnabled;
        internal static ConfigEntry<bool> FocusEnemiesInCombat;
        internal static ConfigEntry<bool> LogGameBindings;
        internal static ConfigEntry<bool> LogScanCandidates;
        internal static ConfigEntry<bool> LogNavigation;
        internal static ConfigEntry<bool> LogFrameBudget;
        internal static ConfigEntry<bool> LogFollowerWheel;

        /// <summary>Rewired is not ready during Awake, so the binding dump waits a moment.</summary>
        private float _bindingDumpAt;
        private bool _bindingsDumped;
        internal static ConfigEntry<KeyCode> KeyRepeatLast;
        internal static ConfigEntry<KeyCode> KeySilence;
        internal static ConfigEntry<KeyCode> KeyDiagnostics;
        internal static ConfigEntry<KeyCode> KeyWhereAmI;
        internal static ConfigEntry<KeyCode> KeyReadPanel;
        internal static ConfigEntry<KeyCode> KeyLogMarker;
        internal static ConfigEntry<KeyCode> KeyNearestValidCell;
        internal static ConfigEntry<KeyCode> KeyCultStatus;
        internal static ConfigEntry<KeyCode> KeyFollowerStatus;
        internal static ConfigEntry<bool> AnnounceCultStatus;
        internal static ConfigEntry<bool> AnnounceCultInWhereAmI;
        internal static ConfigEntry<bool> AnnounceFollowerRequests;
        internal static ConfigEntry<bool> AutoReadPanels;
        internal static ConfigEntry<KeyCode> KeyNextTarget;
        internal static ConfigEntry<KeyCode> KeyPrevTarget;
        internal static ConfigEntry<KeyCode> KeyRescan;
        internal static ConfigEntry<KeyCode> AltNextTarget;
        internal static ConfigEntry<KeyCode> AltPrevTarget;
        internal static ConfigEntry<KeyCode> AltCategory;
        internal static ConfigEntry<KeyCode> AltTrack;
        internal static ConfigEntry<KeyCode> AltWhereIsIt;
        internal static ConfigEntry<KeyCode> KeyCategory;
        internal static ConfigEntry<KeyCode> KeyEnemies;
        internal static ConfigEntry<KeyCode> KeyBeacon;
        internal static ConfigEntry<float> EnemyRange;
        internal static ConfigEntry<KeyCode> KeyTrack;
        internal static ConfigEntry<KeyCode> KeyWhereIsIt;
        internal static ConfigEntry<KeyCode> KeyAutowalk;
        internal static ConfigEntry<bool> AutowalkAvailable;
        internal static ConfigEntry<float> AutoAnnounceInterval;
        internal static ConfigEntry<float> ScanRadius;
        internal static ConfigEntry<bool> SuppressSkeletonLodSpam;
        internal static ConfigEntry<bool> VideoWatchdogEnabled;
        internal static ConfigEntry<float> VideoStallTimeout;
        internal static ConfigEntry<bool> SkipAllVideos;

        private Harmony _harmony;
        private bool _patchesApplied;
        private bool _updateSeen;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            PluginDir = Path.GetDirectoryName(Info.Location) ?? Paths.PluginPath;

            SpeechEnabled = Config.Bind("Speech", "Enabled", true,
                "Master switch for all spoken output.");
            Verbosity = Config.Bind("Speech", "Verbosity", UI.Verbosity.Normal,
                "Low: labels only. Normal: adds role, state and position. " +
                "Diagnostic: adds component and object names, for reporting bugs.");
            AnnounceMenuContext = Config.Bind("Speech", "AnnounceMenuContext", true,
                "Say the menu name when focus moves into a different menu.");
            KeyHelp = Config.Bind("Hotkeys", "Help", KeyCode.F1,
                "Speak the mod's key assignments, one section per press.");
            KeyConfigMenu = Config.Bind("Hotkeys", "SettingsMenu", KeyCode.F2,
                "Open the mod's own spoken settings menu, including the Learn Sounds " +
                "walkthrough. Arrow keys move and change values, Enter activates, Backspace " +
                "goes back. The game is not paused while it is open.");

            AnnounceInteractionPrompts = Config.Bind("Speech", "AnnounceInteractionPrompts", true,
                "Announce the prompt when you are close enough to interact with something, " +
                "such as \"Kneel to be Sacrificed\" or \"Harvest\".");

            AnnounceBarks = Config.Bind("Speech", "AnnounceBarks", true,
                "Read ambient one-liners from followers as you walk past. Queued rather than " +
                "interrupting, so they never cut off story dialogue. Turn off if the base gets chatty.");
            AnnounceNotifications = Config.Bind("Speech", "AnnounceNotifications", true,
                "Read item pickups and transient status cards such as follower, quest, building, " +
                "and ritual notifications.");
            AnnouncePlayerStateChanges = Config.Bind("Speech", "AnnouncePlayerStateChanges", true,
                "Announce live health and fervour gains, losses, spending, and refills. " +
                "Rapid changes settle into one final value.");
            CookingCueLead = Config.Bind("Minigames", "CookingCueLeadSeconds", 0.18f,
                "How far ahead of the cooking success window the timing chirp sounds, in " +
                "seconds. A cue at the window's edge is always late by the time you hear it " +
                "and press, so it needs a reaction allowance. Higher recipe tiers move faster " +
                "and have narrower windows. If you are landing undercooked, lower this; if you " +
                "are landing burned, raise it. The chosen value and the live window are " +
                "recorded as [cooking window] in the log.");
            AnnounceSermonResult = Config.Bind("Speech", "AnnounceSermonResult", true,
                "Announce what a sermon earned: the doctrine category it fed, the experience " +
                "gained and the progress toward the next upgrade, and how many followers " +
                "attended. The game delivers all of this visually, through a bar and a " +
                "follower overlay, so without this the only sound at the end of a sermon is " +
                "the altar menu reappearing.");
            AnnounceHelpHinder = Config.Bind("Speech", "AnnounceHelpHinder", true,
                "Say that the base gate is held shut while Twitch chat votes on a Help or " +
                "Hinder event. Only meaningful with the Twitch integration connected. The vote " +
                "itself and its outcome are announced by the game's own notifications, which " +
                "the mod already reads; this adds only the locked gate, which nothing else " +
                "explains.");
            AnnounceReeducation = Config.Bind("Speech", "AnnounceReeducation", true,
                "Announce progress when a dissenting follower is re-educated, with how much " +
                "dissent is left and how many more daily sessions it will take. The game " +
                "delivers this entirely as an invisible number, so without it a seven-day " +
                "course sounds identical to nothing happening. The number's slow rise while a " +
                "follower dissents stays silent; only re-education is announced.");
            AnnounceChoreProgress = Config.Bind("Speech", "AnnounceChoreProgress", true,
                "Announce chore level-ups, the progression that upgrades the Lamb's mop as " +
                "you clean up messes. The game shows this only as a bar and an animation. " +
                "Individual experience awards stay silent; they fire on every mess cleaned.");
            AnnounceDayCycle = Config.Bind("Speech", "AnnounceDayCycle", true,
                "Announce each new in-game day, naming the once-per-day actions it restores " +
                "such as the Sermon, and announce nightfall. Ordinary phase changes are " +
                "deliberately silent: there are five a day and they govern ambience rather " +
                "than anything you can act on. The where-am-I key always reports the current " +
                "day, phase and time until the next phase.");
            AnnounceOnboardingPhase = Config.Bind("Speech", "AnnounceOnboardingPhase", true,
                "Say the cult tutorial's next step when it changes, such as indoctrinating a " +
                "follower or building a Shrine. This is the game's own progression state, not a " +
                "guess; the where-am-I key reports it too. Loading a save never narrates it.");
            BrailleEnabled = Config.Bind("Speech", "Braille", true,
                "Mirror announcements to a refreshable braille display. Requires a screen reader " +
                "that exposes braille (NVDA does); harmless with no display attached.");

            KeyRepeatLast = Config.Bind("Hotkeys", "RepeatLast", KeyCode.F11,
                "Re-speak the most recent announcement.");
            KeySilence = Config.Bind("Hotkeys", "Silence", KeyCode.F10,
                "Stop speech immediately.");
            KeyDiagnostics = Config.Bind("Hotkeys", "Diagnostics", KeyCode.F9,
                "Speak which speech backend is in use. Useful when setup looks wrong.");
            KeyWhereAmI = Config.Bind("Hotkeys", "WhereAmI", KeyCode.F12,
                "Re-read the current placement cell while building, the focused menu item, " +
                "or live health, fervour, and tarot count in the world.");
            KeyLogMarker = Config.Bind("Hotkeys", "MarkLog", KeyCode.F7,
                "Stamp a numbered marker into the log at this instant, with your position, " +
                "the game's location and the current objective step. Press it the moment " +
                "something happens that the mod did not announce; the marker makes that exact " +
                "moment findable afterwards instead of having to reconstruct it.");
            KeyNearestValidCell = Config.Bind("Hotkeys", "NearestValidCell", KeyCode.F5,
                "While placing a building, say which way the closest cell is that the " +
                "structure would actually fit in. The grid refuses cells for reasons that are " +
                "invisible and often come from a neighbouring square rather than the one under " +
                "the cursor, so without this the only way to find a spot is to walk the cursor " +
                "outward and listen. Reports a direction; it does not move the cursor.");

            KeyReadPanel = Config.Bind("Hotkeys", "ReadPanel", KeyCode.F8,
                "Read the body text of the panel currently open: tutorial explanations, " +
                "popup text, anything not attached to a button.");

            // F3 and F4 rather than more of the where-am-I sentence. The survival readout is
            // already five clauses long and is pressed constantly; four cult bars and a full
            // follower reading do not belong inside something asked that often. Both keep
            // their physical position on every keyboard layout, and both sit with the other
            // reading keys.
            KeyCultStatus = Config.Bind("Hotkeys", "CultStatus", KeyCode.F3,
                "Speak the cult's faith, food, cleanliness and warmth, and how many followers " +
                "you have. The game draws these as coloured bars with no text at all, and all " +
                "four read the same way round: full is good. Bars you have not unlocked yet " +
                "are left out.");
            KeyFollowerStatus = Config.Bind("Hotkeys", "FollowerStatus", KeyCode.F4,
                "Speak everything about one follower: their loyalty, food and health, what is " +
                "wrong with them, and what they are doing. Describes the follower selected in " +
                "the target list, or the nearest one if the selection is something else.");
            AnnounceCultStatus = Config.Bind("Speech", "AnnounceCultStatus", true,
                "Say when a cult bar drops below a quarter full, or climbs back. This is not " +
                "cosmetic: below that line the game starts turning a random follower into a " +
                "dissenter, a starving one, or a sick one, and it warns sighted players by " +
                "making the bar pulse. Also reports when a ritual locks a bar so it cannot " +
                "change.");
            AnnounceCultInWhereAmI = Config.Bind("Speech", "AnnounceCultInWhereAmI", true,
                "Add a short 'faith low' style clause to the where-am-I key while a cult bar " +
                "is below a quarter. Adds nothing at all when every bar is healthy. Turn it " +
                "off to keep that key to your own health and fervour.");
            AnnounceFollowerRequests = Config.Bind("Speech", "AnnounceFollowerRequests", true,
                "Say what a follower is asking for when they put a speech bubble over their " +
                "head. The bubble is an icon and a sound with no text at all, so the sound " +
                "already reaches you and its meaning does not. A follower who has walked over " +
                "to find you is named along with the reason the game recorded — hungry, " +
                "homeless, ill, ready to level up, holding a finished quest. Only followers " +
                "within the scan radius are reported, and each one repeats at most once every " +
                "45 seconds however long they keep asking.");

            // Punctuation and bracket keys, deliberately: this game binds gameplay to
            // letters and the mod must never steal one. These are provisional until the
            // Rewired binding dump (Diagnostics/LogGameBindings) confirms a safe set.
            KeyNextTarget = Config.Bind("Navigation", "NextTarget", KeyCode.RightBracket,
                "Cycle to the next nearby interactable and announce name, distance and direction.");
            KeyPrevTarget = Config.Bind("Navigation", "PreviousTarget", KeyCode.LeftBracket,
                "Cycle to the previous nearby interactable.");
            // Second bindings on the navigation cluster, in the shape screen-reader users
            // already know from document and list navigation: page up and down to move
            // through items, control plus them to change grouping, home and end for the
            // actions at either end.
            //
            // They are not only a convenience. The punctuation defaults are chosen by
            // *character*, and on a QWERTZ or AZERTY keyboard the key producing a semicolon
            // is somewhere else entirely, so the carefully placed cluster stops being a
            // cluster. These keys sit in the same physical place on every layout, which
            // makes them the reliable set rather than the alternative one.
            AltNextTarget = Config.Bind("Navigation", "NextTargetAlternate", KeyCode.PageDown,
                "Second key for the next nearby target. Page Down is where a screen-reader " +
                "user expects to move forward, and unlike the punctuation keys it is in the " +
                "same place on every keyboard layout.");
            AltPrevTarget = Config.Bind("Navigation", "PreviousTargetAlternate", KeyCode.PageUp,
                "Second key for the previous nearby target.");
            AltCategory = Config.Bind("Navigation", "CycleCategoryAlternate", KeyCode.PageDown,
                "Held with Control, steps the target filter forward. With Control and Shift " +
                "together it steps backward. Uses the same key as the alternate next-target " +
                "binding, distinguished by the modifier.");
            AltTrack = Config.Bind("Navigation", "TrackTargetAlternate", KeyCode.Home,
                "Second key for starting or stopping walking guidance.");
            AltWhereIsIt = Config.Bind("Navigation", "AnnounceGuidanceAlternate", KeyCode.End,
                "Second key for repeating the current walking direction.");

            KeyRescan = Config.Bind("Navigation", "Rescan", KeyCode.Backslash,
                "Re-scan surroundings for interactables.");
            KeyCategory = Config.Bind("Navigation", "CycleCategory", KeyCode.Slash,
                "Step the target filter: everything, travel and exits, actions now, facilities, " +
                "resources, story and quests, enemies, followers, and characters. " +
                "Everything and Actions Now collapse repeated natural-resource nodes; Resources " +
                "remains exhaustive.");
            KeyTrack = Config.Bind("Navigation", "TrackTarget", KeyCode.Semicolon,
                "Start walking guidance to the selected target, or stop it if already guiding.");
            KeyWhereIsIt = Config.Bind("Navigation", "AnnounceGuidance", KeyCode.Quote,
                "Speak the current walking direction immediately.");
            // Delete rather than a ninth punctuation key: the cluster the other walking
            // commands use is full, and Delete keeps its physical position on every keyboard
            // layout, sits with the Home and End bindings this pairs with, and is not the
            // NVDA modifier the way Insert is.
            KeyAutowalk = Config.Bind("Navigation", "Autowalk", KeyCode.Delete,
                "Walk automatically along the route guidance is announcing, and stop again " +
                "if pressed a second time. Starts guidance to the selected target first if " +
                "it is not already running. Your own movement keys always take priority " +
                "while held; autowalk resumes when you let go.");
            AutowalkAvailable = Config.Bind("Navigation", "AutowalkAvailable", true,
                "Whether the autowalk key does anything. Autowalk never engages on its own " +
                "and always needs the key pressed for that journey, so this is here for " +
                "players who would rather the key could not be hit by accident, or who do " +
                "not want the mod able to move their character at all.");
            AutoAnnounceInterval = Config.Bind("Navigation", "AutoAnnounceInterval", 3f,
                "Seconds between automatic guidance updates while guiding. Raise it if the " +
                "repetition gets tiring; guidance is always available on demand.");
            ScanRadius = Config.Bind("Navigation", "ScanRadius", 40f,
                "How far to look for interactables when scanning, in world units.");

            KeyEnemies = Config.Bind("Combat", "AnnounceEnemies", KeyCode.Period,
                "Speak a roster of nearby enemies, nearest first, with distance and direction.");
            KeyBeacon = Config.Bind("Combat", "CycleBeacon", KeyCode.Comma,
                "Point the audio beacon at the next nearby enemy. Cycling past the last one " +
                "turns the beacon off.");
            EnemyRange = Config.Bind("Combat", "EnemyRange", 30f,
                "How far to look for enemies, in world units.");
            Combat.CombatConfiguration.Bind(Config);
            Audio.AudioConfiguration.Bind(Config);

            AutoReadPanels = Config.Bind("Speech", "AutoReadPanels", true,
                "Read a panel's body text automatically when it opens, shortly after the open " +
                "animation. Turn off to read panels only on demand with the ReadPanel hotkey.");

            LogGameBindings = Config.Bind("Diagnostics", "LogGameBindings", true,
                "Once per run, log every keyboard key the game itself binds. Bindings live in " +
                "Rewired data rather than code, so this is the only way to pick mod hotkeys that " +
                "cannot collide with gameplay. Log only, never spoken.");

            LogFrameBudget = Config.Bind("Diagnostics", "LogFrameBudget", true,
                "Measure how long the mod's own per-frame work takes and log a summary every " +
                "ten seconds, but only when something exceeded four milliseconds. This is how " +
                "a report that the game felt laggy becomes a number. It cannot see the Harmony " +
                "patch handlers, which run inside the game's own call stack; if these numbers " +
                "stay small while the game still stutters, that points away from the mod's " +
                "update loop. Log only, never spoken.");
            LogFollowerWheel = Config.Bind("Diagnostics", "LogFollowerWheel", true,
                "Record what state a follower was in when their command wheel opened, which " +
                "commands it offered, and which doctrines are unlocked. The game builds a " +
                "different wheel for a sleeping, drunk, dissenting or zombie follower, so a " +
                "command you cannot find may be locked or may simply be on another wheel, and " +
                "the two look identical from the outside. Log only, never spoken.");

            LogScanCandidates = Config.Bind("Diagnostics", "LogScanCandidates", true,
                "When scanning, log characters that had to be named from their object name because " +
                "no label existed. Use this to identify story NPCs the scanner names badly or misses.");

            LogNavigation = Config.Bind("Diagnostics", "LogNavigation", true,
                "Log tracked player, target, waypoint and beacon coordinates once per second, every " +
                "beacon target change, route-area transition, recognized graph blocker, door lock " +
                "state, interaction availability, and primary input. Log only, never spoken; " +
                "disable after navigation is verified.");

            LogDialogue = Config.Bind("Diagnostics", "LogDialogue", true,
                "Log every dialogue line the mod sees, with which hook caught it and what text it " +
                "extracted. The only way to tell 'the hook never fired' apart from 'the hook fired " +
                "and found nothing'. Turn off once dialogue is known good.");

            LogSpeech = Config.Bind("Diagnostics", "LogSpeech", true,
                "Log every line handed to the screen reader, after cleaning, and every line " +
                "dropped as a repeat. This is the only diagnostic that answers 'what did I " +
                "actually hear?' — announcers log from their own side in their own wording, " +
                "and most do not log at all, so a report about what was spoken could not be " +
                "checked against anything. Log only, never spoken.");

            LogCueAudio = Config.Bind("Diagnostics", "LogCueAudio", true,
                "Log a once-a-second census of which sound cues actually played, with a " +
                "count and the loudest volume for each. The companion to LogSpeech for the " +
                "non-speech half: without it a cue logged once when it first ever sounded " +
                "and never again, so 'was the enemy cue audible in that room?' had no answer " +
                "in the log. Log only, never spoken.");

            WarnOnNonStereoOutput = Config.Bind("Speech", "WarnOnNonStereoOutput", true,
                "Say so at startup when Windows is giving the game a surround output rather " +
                "than stereo. Every direction cue this mod makes is stereo pan, so a surround " +
                "mix blurs them, and the game's own positional sounds can be routed to " +
                "speakers you do not have while music keeps playing. Windows can report this " +
                "after spatial sound has been switched back off, so it happens without you " +
                "changing anything. Turn off if you are deliberately running surround.");

            LogCurseAim = Config.Bind("Diagnostics", "LogCurseAim", true,
                "Log one line per curse cast: which input device the game thought was last " +
                "used, whether it acquired an auto-aim target, where the shot went, and how " +
                "far that was from the nearest enemy. The game only acquires an auto-aim " +
                "target when it believes the last active controller was the keyboard, which " +
                "would mean a controller player never gets it — and that cannot be tested by " +
                "hand, because firing with the pad changes the very flag being tested. " +
                "Log only, never spoken.");

            AnnounceMapReveals = Config.Bind("Speech", "AnnounceMapReveals", true,
                "Say which place has been revealed when the world map opens itself to show " +
                "you somewhere new. That reveal is a cutscene rather than a menu — nothing " +
                "is focused and the only text rides an animation — so without this the whole " +
                "event passes with just the window's title and then silence.");

            LogSilentSequences = Config.Bind("Diagnostics", "LogSilentSequences", true,
                "Log scripted moments that hold the camera on something and then say nothing. " +
                "The game marks progress that way — a chain breaking on the dungeon door, a " +
                "location revealed on the map — and those carry no text for a screen reader " +
                "to find, so they reach the player as silence. There are around a hundred " +
                "places that could do it; this reports the ones you actually meet. " +
                "Log only, never spoken.");

            AnnounceChainBreaks = Config.Bind("Speech", "AnnounceChainBreaks", true,
                "Say when a chain breaks on the dungeon door, and how many of the five are " +
                "left. The game marks it with a twelve-second camera hold, a sound and an " +
                "animation, and no text at all, so without this one of the few events that " +
                "opens up the rest of the game passes in silence. It fires when you walk near " +
                "the door after beating a bishop, which can be long after the fight.");

            LogFocus = Config.Bind("Diagnostics", "LogFocus", true,
                "Log every menu focus change: which adapter described the control, what was " +
                "spoken, and the component chain around it. Menus are the one area a log could " +
                "not report on before, so a screen that read badly and a screen never opened " +
                "looked identical afterwards. Search the log for 'adapter=generic' to find " +
                "controls still being read by fallback. Log only, never spoken.");

            SuppressSkeletonLodSpam = Config.Bind("Workarounds", "SuppressSkeletonLodSpam", true,
                "Skip SkeletonAnimationLODGlobalManager.Update until UIManager exists. Works around a " +
                "pre-existing game bug that throws a NullReferenceException every frame during startup, " +
                "flooding the log we rely on for diagnostics. Set false to see the vanilla behaviour.");

            VideoWatchdogEnabled = Config.Bind("Video", "WatchdogEnabled", true,
                "Force a stalled intro video to finish so startup can continue. The game's own skip " +
                "handler is unreachable when playback never starts, which otherwise hard-locks the " +
                "game on a black screen with a Skip prompt that does nothing.");
            VideoStallTimeout = Config.Bind("Video", "StallTimeoutSeconds", 6f,
                "How long a video may render no frames before the watchdog ends it. Raise this if a " +
                "video legitimately takes longer than this to start on your machine.");
            SkipAllVideos = Config.Bind("Video", "SkipAllVideos", false,
                "End every video immediately rather than waiting for the watchdog. Intro and update " +
                "videos have no accessible content, so this is a reasonable default to turn on.");

            UI.SelectableDescriber.Verbosity = Verbosity.Value;
            Verbosity.SettingChanged += (_, __) => UI.SelectableDescriber.Verbosity = Verbosity.Value;

            Speaker.BrailleEnabled = BrailleEnabled.Value;
            BrailleEnabled.SettingChanged += (_, __) => Speaker.BrailleEnabled = BrailleEnabled.Value;

            UI.PanelReader.AutoReadPanels = AutoReadPanels.Value;
            AutoReadPanels.SettingChanged += (_, __) => UI.PanelReader.AutoReadPanels = AutoReadPanels.Value;

            Navigation.Navigator.AutoAnnounceInterval = AutoAnnounceInterval.Value;
            AutoAnnounceInterval.SettingChanged +=
                (_, __) => Navigation.Navigator.AutoAnnounceInterval = AutoAnnounceInterval.Value;

            Navigation.WorldScanner.ScanRadius = ScanRadius.Value;
            ScanRadius.SettingChanged += (_, __) => Navigation.WorldScanner.ScanRadius = ScanRadius.Value;

            ApplyAutowalkAvailability();
            AutowalkAvailable.SettingChanged += (_, __) => ApplyAutowalkAvailability();


            // Controller hotkeys. The 2026-08-23 binding probe found exactly one free element
            // on an XInput pad — the left trigger, unbound in both gameplay and menus — so the
            // layer is a hold on that and everything else is a chord with it. Each command is
            // a separate entry rather than one packed string so a bad value names itself, and
            // BepInEx lists the valid elements next to it in the file.
            ControllerLayerEnabled = Config.Bind("ControllerLayer", "Enabled", true,
                "Hold the left trigger to turn the controller into the mod's hotkeys. While " +
                "it is held the game does not see the other buttons, so nothing fires twice; " +
                "the left stick still walks you. Set to false to leave the pad entirely alone.");

            FocusEnemiesInCombat = Config.Bind("ControllerLayer", "FocusEnemiesInCombat", true,
                "When a fight starts, point the target list at the enemies filter, and put it " +
                "back when the room is clear. This is what lets the d-pad mean the same thing " +
                "all the time: left and right step the current filter, and during combat that " +
                "filter is already the enemies. Stepping onto an enemy also points the beacon " +
                "at it. You can still change filter mid-fight; nothing is locked.");

            // Up and down step the list, left and right change which list. That matches how
            // a screen-reader user already moves through documents, where up and down are the
            // items; it is also the way round the player asked for after using the first one.
            BindCommand(ModCommand.PreviousTarget, PadElement.DPadUp,
                "Previous target in the current filter. During combat that is the enemies, " +
                "and landing on one points the beacon at it.");
            BindCommand(ModCommand.NextTarget, PadElement.DPadDown,
                "Next target in the current filter.");
            BindCommand(ModCommand.PreviousCategory, PadElement.DPadLeft,
                "Step the target filter backward. Filters with nothing in them are skipped.");
            BindCommand(ModCommand.NextCategory, PadElement.DPadRight,
                "Step the target filter forward. Filters with nothing in them are skipped.");
            BindCommand(ModCommand.ToggleGuidance, PadElement.A,
                "Start or stop walking guidance to the selected target.");
            BindCommand(ModCommand.EnemyRoster, PadElement.B,
                "Speak the nearby enemies, nearest first.");
            BindCommand(ModCommand.CycleBeacon, PadElement.X,
                "Point the audio beacon at the next enemy; past the last one turns it off.");
            BindCommand(ModCommand.WhereAmI, PadElement.Y,
                "Where am I: time of day and character status, or re-read the current menu row.");
            BindCommand(ModCommand.Rescan, PadElement.LeftShoulder,
                "Re-scan the surroundings for interactables.");
            BindCommand(ModCommand.AnnounceGuidance, PadElement.RightShoulder,
                "Repeat the current walking direction immediately.");
            BindCommand(ModCommand.Help, PadElement.Back,
                "Speak the next section of help.");
            BindCommand(ModCommand.SettingsMenu, PadElement.Start,
                "Open or close the mod's settings menu.");
            BindCommand(ModCommand.Autowalk, PadElement.LeftStickButton,
                "Walk the route automatically, or stop if already walking.");
            BindCommand(ModCommand.Silence, PadElement.RightStickButton,
                "Stop the current announcement.");
            BindCommand(ModCommand.RepeatLast, PadElement.RightTrigger,
                "Repeat the last thing said.");
            BindCommand(ModCommand.CycleBeaconBack, PadElement.None,
                "Point the beacon at the previous enemy. Unbound by default: the d-pad already " +
                "steps enemies in both directions during combat.");
            BindCommand(ModCommand.ReadPanel, PadElement.None,
                "Read the body text of the panel currently open.");
            BindCommand(ModCommand.NearestValidCell, PadElement.None,
                "Say which way the nearest valid building cell is.");
            BindCommand(ModCommand.MarkLog, PadElement.None,
                "Stamp a marker in the log, for reporting a moment.");

            Input.ControllerLayer.Enabled = ControllerLayerEnabled.Value;
            ControllerLayerEnabled.SettingChanged +=
                (_, __) => Input.ControllerLayer.Enabled = ControllerLayerEnabled.Value;

            Combat.CombatLifecycle.FocusEnemiesInCombat = FocusEnemiesInCombat.Value;
            FocusEnemiesInCombat.SettingChanged +=
                (_, __) => Combat.CombatLifecycle.FocusEnemiesInCombat = FocusEnemiesInCombat.Value;

            Diagnostics.AudioOutputInfo.WarnOnNonStereo = WarnOnNonStereoOutput.Value;
            WarnOnNonStereoOutput.SettingChanged +=
                (_, __) => Diagnostics.AudioOutputInfo.WarnOnNonStereo = WarnOnNonStereoOutput.Value;

            UI.ChainBreakAnnouncer.Enabled = AnnounceChainBreaks.Value;
            AnnounceChainBreaks.SettingChanged +=
                (_, __) => UI.ChainBreakAnnouncer.Enabled = AnnounceChainBreaks.Value;

            UI.MapRevealAnnouncer.Enabled = AnnounceMapReveals.Value;
            AnnounceMapReveals.SettingChanged +=
                (_, __) => UI.MapRevealAnnouncer.Enabled = AnnounceMapReveals.Value;

            Diagnostics.SilentSequenceProbe.Enabled = LogSilentSequences.Value;
            LogSilentSequences.SettingChanged +=
                (_, __) => Diagnostics.SilentSequenceProbe.Enabled = LogSilentSequences.Value;

            Diagnostics.CurseAimDiagnostics.Enabled = LogCurseAim.Value;
            LogCurseAim.SettingChanged +=
                (_, __) => Diagnostics.CurseAimDiagnostics.Enabled = LogCurseAim.Value;

            Audio.CuePlayer.LogCensus = LogCueAudio.Value;
            LogCueAudio.SettingChanged += (_, __) => Audio.CuePlayer.LogCensus = LogCueAudio.Value;

            Navigation.WorldScanner.LogUnnamedCharacters = LogScanCandidates.Value;
            LogScanCandidates.SettingChanged +=
                (_, __) => Navigation.WorldScanner.LogUnnamedCharacters = LogScanCandidates.Value;

            Diagnostics.FrameBudget.Enabled = LogFrameBudget.Value;
            LogFrameBudget.SettingChanged +=
                (_, __) => Diagnostics.FrameBudget.Enabled = LogFrameBudget.Value;

            Diagnostics.NavigationDiagnostics.Enabled = LogNavigation.Value;
            LogNavigation.SettingChanged +=
                (_, __) => Diagnostics.NavigationDiagnostics.Enabled = LogNavigation.Value;

            Combat.EnemyRadar.Range = EnemyRange.Value;
            EnemyRange.SettingChanged += (_, __) => Combat.EnemyRadar.Range = EnemyRange.Value;

            UI.FocusAnnouncer.AnnounceMenuContext = AnnounceMenuContext.Value;
            AnnounceMenuContext.SettingChanged +=
                (_, __) => UI.FocusAnnouncer.AnnounceMenuContext = AnnounceMenuContext.Value;

            _bindingDumpAt = Time.unscaledTime + 8f;

            // English only, and deliberately: the game's language cannot be read this early.
            // Both I2 language getters call InitializeIfNeeded, which selects and applies a
            // startup language before the game's own managers and saved settings exist.
            // LanguageDetection adopts the real language later, once the game has chosen it.
            Localization.Strings.Reporter = message => Log.LogWarning(message);
            Localization.Strings.Initialize(PluginDir, "en", "English");
            Localization.LanguageDetection.Reset();

            Speaker.Initialize(PluginDir);

            Building.PlacementAnnouncer.Initialize();
            UI.ObjectiveAnnouncer.Initialize();
            Status.ReeducationAnnouncer.Initialize();
            UI.TwitchAnnouncer.Initialize();
            UI.TarotAnnouncer.Initialize();
            Combat.CombatLifecycle.Initialize();
            Navigation.FollowerTargetRefresh.Initialize();
            Status.DayCycleAnnouncer.Initialize();

            _harmony = new Harmony(Guid);

            // With an early BepInEx entrypoint the chainloader's manager object can be
            // created before any scene exists, and is then destroyed when the first scene
            // loads - taking Update with it, so the plugin announces itself and goes mute.
            // Marking our own object explicitly is cheap and makes the plugin independent of
            // however a given install has BepInEx configured.
            try
            {
                DontDestroyOnLoad(gameObject);
                Log.LogInfo(
                    $"Plugin host: object=\"{gameObject.name}\" scene=\"{gameObject.scene.name}\" " +
                    $"activeInHierarchy={gameObject.activeInHierarchy} enabled={enabled}");
            }
            catch (System.Exception e)
            {
                Log.LogWarning($"Could not mark the plugin object persistent: {e.Message}");
            }

            Log.LogInfo($"CultAccess ready. Speech: {Speaker.ProviderName}. Native dir: {PluginDir}");

            // Announce late enough that a screen reader has settled after the game's
            // own startup noise, but early enough to confirm the mod loaded at all.
            Speaker.Say($"Cult Access loaded, using {Speaker.ProviderName}.");
        }

        /// <summary>
        /// Apply Harmony patches on the first frame rather than in <c>Awake</c>.
        ///
        /// BepInEx's entrypoint is configurable, and the common default hooks
        /// <c>UnityEngine.CoreModule</c>'s <c>Application..cctor</c> — which runs before
        /// Assembly-CSharp is loaded. Patching a method whose declaring assembly is not yet
        /// loaded silently does nothing: <c>PatchAll</c> reports success, no exception is
        /// raised, and none of the patches take effect. That produced a mod that announced
        /// itself on load and was then completely mute, with a clean log.
        ///
        /// By the first Update the game's assemblies are loaded, so this is both safe and
        /// still far earlier than any gameplay. It also makes the mod independent of however
        /// a given tester's BepInEx happens to be configured.
        /// </summary>
        /// <summary>
        /// Turning autowalk off has to stop a walk already in progress as well as refuse the
        /// next one. Announced, because a character that stops moving for a reason the player
        /// did not connect to the switch they just flipped is a bug report.
        /// </summary>
        /// <summary>
        /// One config entry per command, so an unrecognised value names the command it broke
        /// and BepInEx writes the list of valid elements beside it in the file.
        /// </summary>
        private void BindCommand(ModCommand command, PadElement element, string description)
        {
            var entry = Config.Bind(
                "ControllerLayer", command.ToString(), element,
                description + " Held with the left trigger.");

            Input.ControllerLayer.Bind(command, entry.Value);
            entry.SettingChanged += (_, __) => Input.ControllerLayer.Bind(command, entry.Value);
        }

        private static void ApplyAutowalkAvailability()
        {
            Navigation.Autowalk.Available = AutowalkAvailable.Value;
            if (!AutowalkAvailable.Value)
                Navigation.Autowalk.Disengage("turned off in settings", "Autowalk off.");
        }

        private void ApplyPatchesOnce()
        {
            if (_patchesApplied) return;
            _patchesApplied = true;

            try
            {
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                var patched = 0;
                foreach (var method in _harmony.GetPatchedMethods()) { patched++; }
                Log.LogInfo($"Accessibility patches applied to {patched} method(s).");

                if (patched == 0)
                    Log.LogError(
                        "No methods were patched. Menu reading, dialogue and prompts will be " +
                        "silent; only direct hotkeys will work.");
            }
            catch (System.Exception e)
            {
                // A game update or an optional screen adapter must not take the plugin's
                // direct hotkey features down with it. Harmony applies classes in sequence,
                // so retain the compatible patches already installed; the failing target
                // remains explicit in the log for repair.
                Log.LogError(
                    $"One or more accessibility patches failed; direct hotkeys remain active: {e}");
            }
        }

        private void Update()
        {
            if (!_updateSeen)
            {
                _updateSeen = true;
                Log.LogInfo("Update loop is running.");
            }

            ApplyPatchesOnce();

            if (!_bindingsDumped && LogGameBindings.Value && Time.unscaledTime >= _bindingDumpAt)
            {
                _bindingsDumped = true;
                Diagnostics.BindingDump.LogKeyboardBindings();
                Diagnostics.BindingDump.LogControllerBindings();
            }

            // Shares the binding dump's delay for the same reason: the game's own subsystems
            // need to have finished starting before either can report anything truthful.
            if (Time.unscaledTime >= _bindingDumpAt)
                Diagnostics.AudioOutputInfo.RunOnce();

            Diagnostics.FrameBudget.Tick();
            var frameStarted = Diagnostics.FrameBudget.Begin();

            // Unconditional and above every early return: the census has to be able to
            // report a second in which nothing played, which is itself the answer to
            // "was anything audible?".
            Audio.CuePlayer.TickCensus();
            Diagnostics.SilentSequenceProbe.Tick();
            UI.ChainBreakAnnouncer.Tick();

            // Above the speech gate and every world-hotkey gate: the layer owns the pad while
            // its trigger is held, and the suppression patches read its state, so a frame in
            // which this did not run would hand the game input the layer had just consumed.
            Input.ControllerLayer.Tick();

            // Split one subsystem per scope. The grouped "status" scope showed a sustained
            // average of several milliseconds a frame, which is a continuous cost rather than
            // a spike and therefore worth pinning to the exact tick responsible.
            var statusStarted = Diagnostics.FrameBudget.Begin();

            var scope = Diagnostics.FrameBudget.Begin();
            Diagnostics.NavigationDiagnostics.Tick();
            Diagnostics.FrameBudget.End("status.navdiag", scope);

            scope = Diagnostics.FrameBudget.Begin();
            UI.ObjectiveAnnouncer.Tick();
            Diagnostics.FrameBudget.End("status.objectives", scope);

            scope = Diagnostics.FrameBudget.Begin();
            UI.TwitchAnnouncer.Tick();
            Diagnostics.FrameBudget.End("status.twitch", scope);

            scope = Diagnostics.FrameBudget.Begin();
            UI.TarotAnnouncer.Tick();
            Diagnostics.FrameBudget.End("status.tarot", scope);

            scope = Diagnostics.FrameBudget.Begin();
            Status.PlayerStateAnnouncer.Tick();
            Diagnostics.FrameBudget.End("status.playerstate", scope);

            scope = Diagnostics.FrameBudget.Begin();
            Localization.LanguageDetection.Tick(PluginDir);
            Status.HelpHinderAnnouncer.Tick();
            Diagnostics.FrameBudget.End("status.language", scope);

            scope = Diagnostics.FrameBudget.Begin();
            Status.OnboardingTracker.Tick();
            Diagnostics.FrameBudget.End("status.onboarding", scope);

            scope = Diagnostics.FrameBudget.Begin();
            Status.DayCycleAnnouncer.Tick();
            Diagnostics.FrameBudget.End("status.daycycle", scope);

            scope = Diagnostics.FrameBudget.Begin();
            Status.ChoreProgressTracker.Tick();
            Diagnostics.FrameBudget.End("status.chores", scope);

            scope = Diagnostics.FrameBudget.Begin();
            Status.CultStatusAnnouncer.Tick();
            Diagnostics.FrameBudget.End("status.cult", scope);

            Diagnostics.FrameBudget.End("status", statusStarted);

            var targetsStarted = Diagnostics.FrameBudget.Begin();
            Building.BuildingTargetRefresh.Tick();
            Navigation.FollowerTargetRefresh.Tick();
            Diagnostics.FrameBudget.End("targets", targetsStarted);

            // Sounds are built the first time they play, and building one costs enough to be
            // heard as a stutter. Doing it a frame at a time while nothing is happening moves
            // that cost out of the moment the cue was needed.
            Audio.SoundPrewarm.Tick();

            var combatStarted = Diagnostics.FrameBudget.Begin();
            Combat.CombatAssist.Tick();
            Diagnostics.FrameBudget.End("combat", combatStarted);

            var soundscapeStarted = Diagnostics.FrameBudget.Begin();
            Audio.Soundscape.Tick();
            Diagnostics.FrameBudget.End("soundscape", soundscapeStarted);

            // The settings menu is handled before anything else, so a key can never act in
            // two places on the same frame - and deliberately above the speech switch. Its
            // first row is that switch, so gating the menu behind it would let a player turn
            // speech off and be left with no working way to turn it back on.
            if (Input.Keys.Down(KeyConfigMenu.Value))
                UI.ConfigMenu.Toggle();

            UI.ConfigMenu.Tick();

            if (!SpeechEnabled.Value) return;

            if (Input.Keys.Down(KeyHelp.Value) && !UI.ConfigMenu.AnnounceHelp())
                Help.SpeakNext();

            if (Input.Keys.Down(KeySilence.Value))
                Speaker.Silence();

            if (Input.Keys.Down(KeyRepeatLast.Value))
                Speaker.RepeatHistory();

            if (Input.Keys.Down(KeyDiagnostics.Value))
            {
                var braille = !Speaker.BrailleAvailable
                    ? "braille not supported by this backend"
                    : BrailleEnabled.Value ? "braille on" : "braille off in config";

                Speaker.Say($"Speech backend {Speaker.ProviderName}. {braille}. " +
                            $"{Speaker.HistoryCount} lines in history.");
            }

            if (Input.Keys.Down(KeyWhereAmI.Value))
            {
                if (UI.ConfigMenu.AnnounceCurrent())
                {
                    // The settings menu is a screen of its own; where-am-I re-reads the row.
                }
                else if (UI.FollowerWheelAnnouncer.BlocksWorldHotkeys)
                    UI.FollowerWheelAnnouncer.AnnounceCurrent();
                else if (UI.RadialMenuAnnouncer.AnnounceCurrent())
                {
                    // Weapon, curse, and doctrine wheels bypass normal menu focus.
                }
                else if (Building.PlacementAnnouncer.IsActive)
                    Building.PlacementAnnouncer.AnnounceCurrent();
                else if (UI.TarotAnnouncer.AnnounceStandalone())
                {
                    // The legacy one-card presentation is not a UIMenuBase, but still
                    // owns F12 while visible.
                }
                else if (Lamb.UI.UIMenuBase.ActiveMenus == null ||
                         Lamb.UI.UIMenuBase.ActiveMenus.Count == 0)
                    Status.PlayerStateAnnouncer.AnnounceCurrent();
                else
                    UI.FocusAnnouncer.SpeakCurrentFocus();
            }

            if (Input.Keys.Down(KeyReadPanel.Value))
                UI.PanelReader.ReadTopmostPanel();

            // Deliberately outside the world-hotkey gate: the moment worth marking is often
            // exactly when a modal screen has taken over and nothing else responds.
            if (Input.Keys.Down(KeyLogMarker.Value))
                Diagnostics.LogMarker.Mark();

            if (Input.Keys.Down(KeyNearestValidCell.Value))
                Building.NearestValidCell.Announce();

            // Outside the world-hotkey gate on purpose. Both readouts are about state rather
            // than about the world in front of you, and the cult bars are exactly what a
            // player wants while a menu or a card is up.
            if (Input.Keys.Down(KeyCultStatus.Value))
                Status.CultStatusAnnouncer.AnnounceCurrent();

            if (Input.Keys.Down(KeyFollowerStatus.Value))
                Status.FollowerAnnouncer.AnnounceSelected();

            var worldHotkeysAllowed = !UI.ConfigMenu.IsOpen &&
                                      !UI.FollowerWheelAnnouncer.BlocksWorldHotkeys &&
                                      !UI.RadialMenuAnnouncer.BlocksWorldHotkeys;
            if (worldHotkeysAllowed)
            {
                if (Input.Keys.Down(KeyRescan.Value))
                {
                    // Scoped separately: a world scan walks the hierarchy and is the obvious
                    // candidate for the large isolated spikes, but it is driven by a keypress
                    // rather than by the frame, so averaging it in would hide it.
                    var scanStarted = Diagnostics.FrameBudget.Begin();
                    Navigation.Navigator.Refresh();
                    Diagnostics.FrameBudget.End("scan", scanStarted);
                }

                // Control decides between stepping the filter and stepping the target on
                // the alternate keys, so the pair has to be resolved together rather than as
                // independent ifs; otherwise Control plus Page Down would do both.
                var control = Input.Keys.Control;

                if (Input.Keys.Down(KeyCategory.Value) ||
                    (control && Input.Keys.Down(AltCategory.Value)))
                    Navigation.Navigator.CycleCategory(Input.Keys.Shift ? -1 : 1);
                else if (Input.Keys.Down(AltNextTarget.Value))
                    Navigation.Navigator.Cycle(1);
                else if (Input.Keys.Down(AltPrevTarget.Value))
                    Navigation.Navigator.Cycle(-1);

                if (Input.Keys.Down(KeyNextTarget.Value))
                    Navigation.Navigator.Cycle(1);

                if (Input.Keys.Down(KeyPrevTarget.Value))
                    Navigation.Navigator.Cycle(-1);

                if (Input.Keys.Down(KeyTrack.Value) || Input.Keys.Down(AltTrack.Value))
                    Navigation.Navigator.ToggleTracking();

                if (Input.Keys.Down(KeyWhereIsIt.Value) || Input.Keys.Down(AltWhereIsIt.Value))
                    Navigation.Navigator.AnnounceGuidance();

                if (Input.Keys.Down(KeyAutowalk.Value))
                    Navigation.Autowalk.Toggle();

                if (Input.Keys.Down(KeyEnemies.Value))
                    Combat.EnemyRadar.AnnounceHostiles();

                if (Input.Keys.Down(KeyBeacon.Value))
                    Combat.EnemyRadar.CycleBeaconTarget();
            }

            var uiStarted = Diagnostics.FrameBudget.Begin();
            UI.PanelReader.Tick();
            UI.InteractionPromptAnnouncer.Tick();
            UI.NotificationAnnouncer.Tick();
            UI.FollowerWheelAnnouncer.Tick();
            UI.RadialMenuAnnouncer.Tick();
            Building.PlacementAnnouncer.Tick();
            Diagnostics.FrameBudget.End("ui", uiStarted);

            if (worldHotkeysAllowed)
            {
                var navigationStarted = Diagnostics.FrameBudget.Begin();
                Navigation.Navigator.Tick();
                // After the navigator, so the heading is the one this frame's route produced.
                // Inside the same gate on purpose: a settings menu or a follower wheel stops
                // this being called, the frame stamp goes stale, and the Lamb stops walking
                // without either subsystem having to know about the other.
                Navigation.Autowalk.Tick();
                Diagnostics.FrameBudget.End("navigation", navigationStarted);

                var radarStarted = Diagnostics.FrameBudget.Begin();
                Combat.EnemyRadar.Tick();
                Audio.Beacon.Tick();
                Diagnostics.FrameBudget.End("radar", radarStarted);
            }

            // Closed last, so "update" really means everything the mod does this frame and
            // not merely the part before the early returns. The named scopes inside it are
            // there to say which part, once the total says there is a part worth finding.
            Diagnostics.FrameBudget.End("update", frameStarted);
        }

        /// <summary>
        /// Stop everything that sustains when the game loses focus or is suspended.
        ///
        /// Necessary because FMOD's mixer keeps running after Unity stops calling Update.
        /// The wall ring and the minigame tone hold channels open and are stopped from the
        /// update loop, so alt-tabbing away left them playing over the desktop indefinitely,
        /// with nothing left running that could ever stop them. One-shot cues cannot do this;
        /// they end on their own.
        ///
        /// Unity delivers both of these on the transition itself, so they arrive even when
        /// the game is configured not to run in the background.
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) SilenceSustainedAudio();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SilenceSustainedAudio();
        }

        private static void SilenceSustainedAudio()
        {
            Audio.Soundscape.Silence();
            Audio.CuePreview.Cancel();
            Audio.TimingTone.Stop();
        }

        private void OnDestroy()
        {
            UI.TwitchAnnouncer.Shutdown();
            UI.TarotAnnouncer.Shutdown();
            UI.RadialMenuAnnouncer.Shutdown();
            Status.PlayerStateAnnouncer.Shutdown();
            Status.OnboardingTracker.Shutdown();
            Status.DayCycleAnnouncer.Shutdown();
            Status.ChoreProgressTracker.Shutdown();
            Status.CultStatusAnnouncer.Shutdown();
            Status.FollowerAttentionAnnouncer.Shutdown();
            UI.ObjectiveAnnouncer.Shutdown();
            Building.PlacementAnnouncer.Shutdown();
            Building.BuildingTargetRefresh.Shutdown();
            Navigation.FollowerTargetRefresh.Shutdown();
            Audio.SoundPrewarm.Reset();
            UI.MinigameTimingCues.Shutdown();
            UI.ConfigMenu.Shutdown();
            Navigation.Navigator.Shutdown();
            Audio.Beacon.Shutdown();
            Audio.Soundscape.Reset();
            Combat.CombatAssist.Shutdown();
            _harmony?.UnpatchSelf();
            Speaker.Shutdown();
        }
    }
}
