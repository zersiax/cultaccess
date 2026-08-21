using System.Collections.Generic;
using CultAccess.Diagnostics;
using CultAccess.Speech;
using Rewired;

namespace CultAccess
{
    /// <summary>
    /// Speaks the current key assignments on demand.
    ///
    /// Built from the live config entries rather than a hardcoded list, so it stays correct
    /// after any rebind — a help text that can drift out of date is worse than none, because
    /// it teaches the wrong keys with authority.
    ///
    /// Split into sections read one press at a time. Reading twenty bindings as a single
    /// utterance is technically complete and practically useless: nobody retains item
    /// fourteen, and interrupting to re-hear it means starting over.
    /// </summary>
    internal static class Help
    {
        private static int _section;

        public static void SpeakNext()
        {
            if (UI.FollowerWheelAnnouncer.BlocksWorldHotkeys)
            {
                UI.FollowerWheelAnnouncer.AnnounceHelp();
                return;
            }

            if (UI.RadialMenuAnnouncer.AnnounceHelp()) return;

            var sections = BuildSections();
            if (sections.Count == 0) return;

            if (_section >= sections.Count) _section = 0;

            var section = sections[_section];
            var position = $"help {_section + 1} of {sections.Count}";
            _section++;

            Speaker.Say($"{position}. {section}");
        }

        /// <summary>Start again from the first section, e.g. when a new screen is entered.</summary>
        public static void Reset() => _section = 0;

        private static List<string> BuildSections()
        {
            var menuLeft = GameKey(35, "left", Pole.Negative);
            var menuRight = GameKey(35, "right", Pole.Positive);
            var confirm = GameKey(38, "confirm");
            var previousTab = GameKey(43, "page left");
            var nextTab = GameKey(44, "page right");

            return new List<string>
            {
                "Walking. Page up and page down step through nearby things, announcing name, " +
                "distance and direction. That only moves the selection. Home starts guiding " +
                "you to the selected one, and stops guiding if pressed again. End repeats the " +
                "direction immediately. Control with page down cycles the target filter " +
                "forward, and control and shift with page down cycles it backward: " +
                "everything, travel and exits, actions now, facilities, resources, story and " +
                "quests, enemies, followers, and characters. Everything and " +
                "Actions Now group repeated lumber, stone, and food nodes; Resources and All " +
                "Targets list every one. " +
                $"{Key(Plugin.KeyRescan)} re-scans, which you need after the world changes.",

                "The same walking commands are also on a punctuation cluster, which may suit " +
                "you better if your hands are already there: " +
                $"{Key(Plugin.KeyPrevTarget)} and {Key(Plugin.KeyNextTarget)} step through targets, " +
                $"{Key(Plugin.KeyTrack)} starts and stops guidance, " +
                $"{Key(Plugin.KeyWhereIsIt)} repeats the direction, and " +
                $"{Key(Plugin.KeyCategory)} cycles the filter forward, or backward with shift. " +
                "Those keys are chosen by the character they type, so on a keyboard that is " +
                "not US English they may not sit together; the page up and page down set is " +
                "in the same place on every layout.",

                "While guiding, keep moving in the announced direction; you do not need to stop and wait. " +
                "A changed heading is announced immediately as a turn, straight route points pass silently, " +
                "and the current instruction repeats every few seconds. Guidance re-routes if you wander off " +
                "and says when you arrive. " +
                "North means screen up, so north east means push up and right.",

                "Combat. " +
                $"{Key(Plugin.KeyEnemies)} lists nearby enemies with distance and direction. " +
                $"{Key(Plugin.KeyBeacon)} points the beacon directly at the next enemy, and turns enemy tracking " +
                "off after the last one. An enemy lock takes priority over a walking-guidance beacon. " +
                "The beacon pings: left and right is stereo position. Pitch is how far up or " +
                "down the screen the target is, which in this game means north and south " +
                "across the ground, not height: higher means further up-screen, lower means " +
                "further down. It is not distance. Ping rate is distance, and speeds up as " +
                "you close in, so very fast means you are on top of it. " +
                "The game's own combat keys are K to attack, left shift for heavy attack, " +
                "L to shoot, space to dodge, Q for ability, J for relic, and T for fleece ability.",

                "Combat cues. Anything that buzzes harshly means you are about to take " +
                "damage and should move: a single falling buzz is an enemy winding up a melee " +
                "attack, a high double buzz is a shot predicted to hit you, and a lower double " +
                "buzz is a grenade landing area you are standing in. A metallic rattle means a " +
                "trap has triggered under you and will hurt in about half a second. " +
                "The quieter sounds are reports rather than warnings. A soft band of noise " +
                "marks where a dodge took you, and only when you dodged without movement " +
                "held; a knock after it means that dodge ran into a wall. Two rising notes " +
                "mean the dodge actually saved you, because dodging makes you briefly " +
                "untouchable and a hit was thrown away. A low pulse means a wall is ahead in " +
                "the direction you are moving, speeding up as it closes, and a broad knock " +
                $"means contact. {Key(Plugin.KeyConfigMenu)} then Learn sounds plays every " +
                "one of these with an explanation, and says what to name a file if you want " +
                "to replace it with your own.",

                "Reading. " +
                $"{Key(Plugin.KeyWhereAmI)} re-reads the focused menu item, or current health, " +
                "fervour, active tarot-card count, and the cult tutorial's next step while no " +
                "menu is open. " +
                $"{Key(Plugin.KeyReadPanel)} reads the open panel's body text, such as a tutorial explanation. " +
                $"{Key(Plugin.KeyLogMarker)} stamps a numbered marker in the log; press it when " +
                "something happens that was not announced, so the moment can be found afterwards. " +
                $"{Key(Plugin.KeyNearestValidCell)} while placing a building, says which way the " +
                "nearest cell is that the structure will actually fit in. " +
                $"{Key(Plugin.KeyRepeatLast)} repeats the last thing said. " +
                $"{Key(Plugin.KeySilence)} stops speech.",

                // Named for the game's own settings screens, to keep it distinct from the
                // mod's settings menu section below. Both are spoken, so the titles must not
                // sound the same.
                "The game's settings screens, and Twitch. In menus, W and S move between controls. " +
                $"{menuLeft} and {menuRight} adjust sliders and left-right choices; every changed value is announced. " +
                $"{confirm} toggles switches. Settings has multiple tabs: {previousTab} goes to the previous tab " +
                $"and {nextTab} goes to the next. Twitch Settings, Connect opens browser authorization; " +
                "the channel must be live with Cult of the Lamb selected before the integration becomes active. " +
                "Integration Configuration opens the Twitch extension dashboard.",

                "Base building. Build catalogue entries announce their name, availability, cost, " +
                "description, and list position. During placement, W A S D moves the game's grid " +
                "cursor and each new cell announces its contents and validity. E places, F cancels, " +
                "and R rotates or removes when that action is available. F12 repeats the current cell.",

                "Progression menus. Inventory and player pages identify icon-only items, equipment, " +
                "and effects. The quest log reads every objective and E toggles tracking when allowed. " +
                "Rituals and upgrade trees state exact costs and unmet requirements. On the doctrine " +
                "category wheel, keep a direction held while pressing E. Doctrine and upgrade " +
                "confirmations follow the Hold Actions accessibility setting and announce whether " +
                "E must be held or pressed again.",

                "Settings. " +
                $"{Key(Plugin.KeyConfigMenu)} opens the mod's own settings menu and closes it again. " +
                "Up and down move, left and right change a value, Enter opens a section or " +
                "runs an action, and Backspace goes back. It covers speech, every sound " +
                "individually, wayfinding, and a Learn sounds section that plays each cue and " +
                "says what it means. The game is not paused while it is open, so open it when " +
                "you are safe. Everything in it is also in the config file.",

                "Diagnostics. " +
                $"{Key(Plugin.KeyDiagnostics)} reports the speech backend and whether braille is active. " +
                "Hotkeys are rebindable in the config file, section by section.",
            };
        }

        private static string GameKey(int actionId, string fallback, Pole? pole = null)
        {
            var binding = BindingDump.KeyboardBindingsForAction(actionId, pole);
            return binding.Length > 0 ? binding : fallback;
        }

        /// <summary>Speak punctuation keys as words; NVDA would otherwise read the symbol.</summary>
        internal static string Key(BepInEx.Configuration.ConfigEntry<UnityEngine.KeyCode> entry)
        {
            if (entry == null) return "unbound";

            switch (entry.Value)
            {
                case UnityEngine.KeyCode.LeftBracket: return "left bracket";
                case UnityEngine.KeyCode.RightBracket: return "right bracket";
                case UnityEngine.KeyCode.Backslash: return "backslash";
                case UnityEngine.KeyCode.Semicolon: return "semicolon";
                case UnityEngine.KeyCode.Quote: return "apostrophe";
                case UnityEngine.KeyCode.Comma: return "comma";
                case UnityEngine.KeyCode.Period: return "period";
                case UnityEngine.KeyCode.Slash: return "slash";
                case UnityEngine.KeyCode.Minus: return "minus";
                case UnityEngine.KeyCode.Equals: return "equals";
                default: return entry.Value.ToString();
            }
        }
    }
}
