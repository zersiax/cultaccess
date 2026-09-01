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

                "Controller. Hold the left trigger to turn the pad into the mod's hotkeys. " +
                "It is the only button this game leaves free, so everything is a chord with " +
                "it. While it is held the game does not see the other buttons, so nothing " +
                "fires twice, and the left stick still walks you. " +
                "D-pad up and down step the target filter; left and right step through the " +
                "targets in it. A starts and stops guidance, left stick click is autowalk, " +
                "right shoulder repeats the direction, left shoulder re-scans. " +
                "B lists the enemies, X points the beacon at the next one, Y is where am I, " +
                "right trigger repeats the last thing said, right stick click stops it. " +
                "Back is this help, Start is the settings menu. " +
                "When a fight starts the target filter moves to the enemies by itself and " +
                "moves back when the room is clear, so in combat left and right are already " +
                "stepping enemies and landing on one points the beacon at it. Every one of " +
                "these can be moved in the ControllerLayer section of the config file.",

                $"Autowalk. {Key(Plugin.KeyAutowalk)} walks you along that route instead of " +
                "steering yourself, and stops again if pressed a second time. It starts " +
                "guidance to the selected target first if guidance is not already running, " +
                "so from a chosen target it is the only key you need. It follows the exact " +
                "route rather than the eight compass points the instructions are spoken in, " +
                "so it arrives closer than following the words does. Your own movement keys " +
                "always win while you hold them and autowalk picks up again when you let go, " +
                "which is how you step round something in the way. It stops on arrival, when " +
                "guidance stops, and if it spends three seconds getting nowhere, which it " +
                "says out loud. It does not fight for you: attacking, dodging and everything " +
                "else are yours, and it lets go for as long as any of them is happening. " +
                "Turn the key off entirely under Wayfinding in the settings menu.",

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

                "Enemy proximity. The always-on enemy cue now ticks faster the closer that " +
                "enemy is, the same way the beacon does, and each enemy keeps its own rate. " +
                "So several around you sound like several rhythms at once, and the fastest " +
                "one is the nearest — which is the one to deal with. Left and right is still " +
                "position and pitch is still up or down the screen; only the speed is new. " +
                "It is the rate rather than the volume that tells you distance, because a " +
                "changing rhythm carries through a loud fight and a volume difference does not.",

                "Spawners. An enemy announced as a spawner is generating the others. Killing " +
                "it kills everything it has spawned outright and ends the fight; killing its " +
                "brood achieves nothing and earns no experience. Spawners are always listed " +
                "first, in the enemy roster and in the enemies filter, so you never have to " +
                "step past anything to reach one.",

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

                "Your cult. " +
                $"{Key(Plugin.KeyCultStatus)} reads the four bars the game draws across the top " +
                "of the screen: faith, food, cleanliness and warmth, plus how many followers " +
                "you have. All four read the same way round, so a high number is always good, " +
                "and any bar you have not unlocked is simply left out. Other players call " +
                "these the faith, hunger and sickness bars. Below a quarter full the mod says " +
                "so without being asked, because that is the line where the game starts " +
                "picking a follower at random and making them a dissenter, or starving, or " +
                "ill. Getting back above it is announced too. A ritual can freeze a bar, " +
                "which is also said. " +
                $"{Key(Plugin.KeyWhereAmI)} adds a short reminder while a bar is low and " +
                "nothing at all when they are all healthy.",

                "Followers around you. Stepping the followers filter now tells you who each " +
                "one is and what they need: \"Sinterklaas the level 4 Goat, ill, quest to " +
                "complete\", then the distance and direction. The form and the level are how " +
                "a sighted player tells two followers apart at a glance, and the last part is " +
                "the game's own ranking of what is worth walking over for — protect them from " +
                "lightning, catch them leaving, absolve their sin, complete a quest, collect " +
                "a reward, or answer one who is asking for you. " +
                "Followers also put a speech bubble over their head when they want " +
                "something. That bubble is a picture and a sound with no words in it, so you " +
                "were hearing the sound and nothing else; you now hear who it was and what " +
                "they want, with their distance and direction. One who has walked across the " +
                "base to find you says why — hungry, homeless, ill, ready to level up, " +
                "holding a finished quest. Each follower repeats at most once every 45 " +
                "seconds however long they keep asking, and only inside your scan radius.",

                "One follower. " +
                $"{Key(Plugin.KeyFollowerStatus)} describes a single follower: their loyalty, " +
                "food and health, what is wrong with them, what they are doing right now, " +
                "their traits, marriage and age. It describes whichever follower is selected " +
                "in the target list, so step to one with the followers filter first; if the " +
                "selection is not a follower it falls back to the nearest one. " +
                "The same numbers now read off every follower tile in the game — the roster, " +
                "sacrifices, beds, the daycare, the mating tent — where before they were " +
                "coloured bars with no text beside them, along with the game's own reason a " +
                "follower cannot be chosen on that screen.",

                "Reading. " +
                $"{Key(Plugin.KeyWhereAmI)} re-reads the focused menu item, or current health, " +
                "fervour, active tarot-card count, and the cult tutorial's next step while no " +
                "menu is open. " +
                $"{Key(Plugin.KeyReadPanel)} reads the open panel's body text, such as a tutorial " +
                "explanation, or the statistics on your cult pages, which are numbers with " +
                "pictures beside them that nothing else can reach. " +
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
