using System;
using System.Collections.Generic;

namespace CultAccess.Localization
{
    /// <summary>
    /// Compiled-in English, the guaranteed baseline behind every override file.
    ///
    /// Keys are descriptive and namespaced by feature, never numbered: a translator working
    /// through this list should be able to tell what each line is for without running the
    /// game, and a key that moves feature should be renamed rather than silently reused.
    ///
    /// Placeholders are positional so word order belongs to the translation. Where a sentence
    /// varies by count, singular and plural are separate keys rather than an assembled
    /// fragment.
    /// </summary>
    internal static class StringDefaults
    {
        internal static readonly Dictionary<string, string> English =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Onboarding: what the game is currently waiting for.
                { "onboarding.indoctrinate", "Next step, indoctrinate your new follower" },
                {
                    "onboarding.indoctrinate_at_platform",
                    "Next step, go to the indoctrination platform and indoctrinate your new follower"
                },
                { "onboarding.shrine", "Next step, build a Shrine" },
                {
                    "onboarding.devotion",
                    "Next step, command a follower to worship at the Shrine, then collect Devotion"
                },
                { "onboarding.unknown", "Next step, {0}" },

                // Day and night cycle.
                { "day.new", "Day {0}." },
                { "day.new_with_restored", "Day {0}. {1} available." },
                { "day.nightfall", "Night falls." },
                { "day.status", "Day {0}, {1}" },
                { "day.status_with_countdown", "Day {0}, {1}, about {2} until {3}" },
                { "duration.second", "{0} second" },
                { "duration.seconds", "{0} seconds" },

                // Enterable buildings.
                { "entrance.open", "{0}, entrance, walk in without pressing Interact" },
                { "entrance.blocked", "{0}, entrance, blocked" },
                { "entrance.arrival_open", "At {0}. Walk into the doorway to go inside." },
                { "entrance.arrival_blocked", "Reached {0}. The doorway is blocked." },

                // Dungeon entrances gated on follower count.
                { "dungeon_door.requirement_one", "requires {0} follower; you have {1}" },
                { "dungeon_door.requirement_many", "requires {0} followers; you have {1}" },

                // Settings menu: structure and shared wording.
                { "config.title", "CultAccess settings" },
                { "config.item", "item" },
                { "config.submenu", "submenu" },
                { "config.on", "on" },
                { "config.off", "off" },
                { "config.percent", "{0} percent" },
                { "config.metres", "{0} metres" },
                { "config.seconds", "{0} seconds" },
                { "config.per_second", "{0} per second" },
                { "config.page_empty", "{0}. Nothing to change here." },
                { "config.page_items_one", "1 item" },
                { "config.page_items_many", "{0} items" },
                { "config.closed", "Settings closed." },
                { "config.nothing_to_do", "Nothing to do on this item." },
                { "config.no_detail", "No further explanation for this item." },
                { "config.currently_silent", "currently silent, because a switch above it is off" },
                {
                    "config.controls",
                    "Up and down move. Left and right change a value. Enter opens a section or runs " +
                    "an action. Backspace goes back, and closes the menu from the top. {0} closes " +
                    "it from anywhere. The game keeps running, so open this when you are safe."
                },

                // Settings menu: the top-level sections.
                { "config.speech", "Speech" },
                { "config.speech.detail", "What the mod says out loud, and how much of it." },
                { "config.sounds", "Sounds" },
                { "config.sounds.detail", "Every sound the mod makes, each with its own switch and volume." },
                { "config.wayfinding", "Wayfinding" },
                { "config.wayfinding.detail", "How guidance reaches you while walking, and how far the scanner looks." },
                { "config.learn_sounds", "Learn sounds" },
                { "config.learn_sounds.detail", "Hear every cue and what it means. Moving through the list plays each one." },
                { "config.diagnostics", "Diagnostics" },
                { "config.diagnostics.detail", "Log files for reporting problems. Nothing here is ever spoken." },

                // Settings menu: speech.
                { "config.speech_enabled", "Speech" },
                {
                    "config.speech_enabled.detail",
                    "Master switch for everything the mod says. Sounds are unaffected. The " +
                    "settings menu keeps speaking while this is off, so there is always a way " +
                    "to switch it back on."
                },
                { "config.verbosity", "Detail level" },
                {
                    "config.verbosity.detail",
                    "How much is said about each menu item. Low is the label alone, normal adds " +
                    "role, state and position, and diagnostic adds the object name for bug reports."
                },
                { "config.verbosity_low", "low" },
                { "config.verbosity_normal", "normal" },
                { "config.verbosity_diagnostic", "diagnostic" },
                { "config.braille", "Braille" },
                {
                    "config.braille.detail",
                    "Mirror announcements to a refreshable braille display through your screen " +
                    "reader. Harmless with no display attached."
                },
                { "config.menu_context", "Menu names" },
                { "config.menu_context.detail", "Say the menu name when focus moves into a different menu." },
                { "config.auto_read_panels", "Read panels automatically" },
                {
                    "config.auto_read_panels.detail",
                    "Read a panel's body text as it opens. With this off, panels are read only on " +
                    "demand."
                },
                { "config.interaction_prompts", "Interaction prompts" },
                { "config.interaction_prompts.detail", "Announce the prompt when you are close enough to interact with something." },
                { "config.notifications", "Notifications" },
                {
                    "config.notifications.detail",
                    "Read item pickups and transient status cards such as follower, quest, building " +
                    "and ritual notices."
                },
                { "config.barks", "Follower chatter" },
                {
                    "config.barks.detail",
                    "Read ambient one-liners from followers as you walk past. Queued, so they never " +
                    "cut off story dialogue."
                },
                { "config.player_state", "Health and fervour" },
                {
                    "config.player_state.detail",
                    "Announce live health and fervour gains, losses and refills. Rapid changes " +
                    "settle into one final value."
                },
                { "config.day_cycle", "Day and night" },
                { "config.day_cycle.detail", "Announce each new day and what it restores, and announce nightfall." },
                { "config.chore_progress", "Chore level-ups" },
                { "config.chore_progress.detail", "Announce chore level-ups. Individual experience awards stay silent." },
                { "config.onboarding", "Tutorial next step" },
                { "config.onboarding.detail", "Say the cult tutorial's next step whenever it changes." },
                { "config.combat_state", "Combat start and room clear" },
                { "config.combat_state.detail", "Speak the enemy count when combat starts, and say when the room is clear." },

                // Settings menu: sounds.
                { "config.all_sounds", "All sounds" },
                { "config.all_sounds.detail", "Master switch for every sound the mod makes. Speech is unaffected." },
                { "config.overall_volume", "Overall volume" },
                {
                    "config.overall_volume.detail",
                    "Scales every sound at once, so the whole set can be balanced against speech " +
                    "and the game's own audio. Individual cue volumes stay relative to each other."
                },
                { "config.combat_cues", "Combat cues" },
                {
                    "config.combat_cues.detail",
                    "Short sounds for things that must arrive faster than speech: dodges, walls, " +
                    "incoming attacks and traps."
                },
                { "config.always_on", "Always-on sounds" },
                {
                    "config.always_on.detail",
                    "Continuous quiet sounds for what is near you. Every category starts off. " +
                    "Volume falls away steeply with distance, so things are audible only when they " +
                    "are close."
                },
                { "config.minigame_cues", "Minigame cues" },
                { "config.minigame_cues.detail", "Timing sounds for minigames such as cooking." },
                { "config.wayfinding_cues", "Beacon" },
                { "config.wayfinding_cues.detail", "The repeating ping that locates whatever you are tracking." },
                { "config.all_combat_cues", "All combat cues" },
                {
                    "config.all_combat_cues.detail",
                    "Section switch. Turning it off also stops the detection work behind these " +
                    "cues, so it costs nothing to leave off."
                },
                { "config.all_always_on", "All always-on sounds" },
                {
                    "config.all_always_on.detail",
                    "Section switch for the whole always-on layer, without losing the individual " +
                    "category settings underneath."
                },
                { "config.all_minigame_cues", "All minigame cues" },
                { "config.all_minigame_cues.detail", "Section switch for minigame timing sounds." },
                { "config.all_wayfinding_cues", "Beacon on" },
                { "config.all_wayfinding_cues.detail", "Section switch for the navigation beacon." },
                { "config.ambient_budget", "Most sounds per second" },
                {
                    "config.ambient_budget.detail",
                    "Hard ceiling on how many always-on sounds may play per second across every " +
                    "category. Only reached in a crowded room; when it is, incoming fire keeps " +
                    "sounding and scenery does not."
                },
                { "config.cue_on", "On" },
                { "config.cue_on.detail", "Whether this cue plays at all." },
                { "config.cue_volume", "Volume" },
                { "config.cue_volume.detail", "Loudness of this cue, before the overall volume is applied." },
                { "config.cue_range", "Range" },
                {
                    "config.cue_range.detail",
                    "How far away this can still be heard. Volume falls off steeply inside it, so " +
                    "things stay quiet until you are close; this is the distance at which they go " +
                    "silent."
                },
                { "config.cue_repeat", "Repeat" },
                {
                    "config.cue_repeat.detail",
                    "How long between repeats for one source. Shorter is more present and more " +
                    "tiring."
                },
                { "config.cue_max_at_once", "Most at once" },
                {
                    "config.cue_max_at_once.detail",
                    "How many of these may sound at the same time, nearest first. This is what " +
                    "stops a busy room becoming a wash."
                },
                { "config.play_it", "Play it" },
                { "config.play_it.detail", "Play this cue once, straight ahead." },
                { "config.hear_it_move", "Hear it move" },
                {
                    "config.hear_it_move.detail",
                    "Play this cue at the left, centre and right, then low, level and high. Left " +
                    "and right is stereo position; the pitch change is vertical aim, not distance."
                },

                // Settings menu: wayfinding.
                { "config.guidance_mode", "Guidance" },
                {
                    "config.guidance_mode.detail",
                    "Which channels guide you while walking guidance is running. Refusals, arrivals " +
                    "and the direction key always speak, whichever you choose."
                },
                { "config.mode_beacon_and_speech", "beacon and speech" },
                { "config.mode_beacon_only", "beacon only" },
                { "config.mode_speech_only", "speech only" },
                { "config.autowalk", "Autowalk" },
                {
                    "config.autowalk.detail",
                    "Whether the autowalk key can walk you along the route guidance is " +
                    "announcing. It never starts on its own and always needs the key pressed " +
                    "for that journey, and your own movement keys take priority while held. " +
                    "Turn this off if you would rather the mod could not move your character."
                },
                { "config.beacon_cues", "Beacon sound" },
                { "config.beacon_cues.detail", "The beacon's own switch and volume." },
                { "config.announce_interval", "Repeat guidance every" },
                {
                    "config.announce_interval.detail",
                    "How long between automatic repeats of the current walking instruction. " +
                    "Guidance is always available on demand as well."
                },
                { "config.scan_radius", "Scanner range" },
                { "config.scan_radius.detail", "How far to look for interactable things when scanning." },
                { "config.enemy_range", "Enemy range" },
                { "config.enemy_range.detail", "How far to look for enemies when listing them or locking the beacon." },
                { "config.projectile_horizon", "Projectile warning lead" },
                {
                    "config.projectile_horizon.detail",
                    "How far ahead to warn about a predicted projectile hit. Raise it if warnings " +
                    "arrive too late; lower it if they are too eager."
                },
                { "config.cooking_lead", "Cooking cue lead" },
                {
                    "config.cooking_lead.detail",
                    "How far ahead of the cooking success window the timing cue sounds. Raise it if " +
                    "you are landing burned; lower it if you are landing undercooked."
                },

                // Settings menu: diagnostics. Log only, never spoken.
                { "config.log_navigation", "Log navigation" },
                {
                    "config.log_navigation.detail",
                    "Record tracked positions, routes, blockers and beacon targets in the log file."
                },
                { "config.log_combat", "Log combat" },
                {
                    "config.log_combat.detail",
                    "Record dodge corridors, wall contacts, predicted threats, trap telegraphs and " +
                    "damage in the log file."
                },
                { "config.log_dialogue", "Log dialogue" },
                { "config.log_dialogue.detail", "Record every dialogue line the mod sees and which hook caught it." },
                { "config.log_scan", "Log scan candidates" },
                {
                    "config.log_scan.detail",
                    "Record characters that had to be named from their object name because no label " +
                    "existed."
                },
                { "config.log_bindings", "Log game key bindings" },
                { "config.log_bindings.detail", "Once per run, record every keyboard key the game itself binds." },

                // Cue names and explanations. Used by the menu and by Learn sounds.
                { "cue.beacon", "Beacon" },
                {
                    "cue.beacon.detail",
                    "A repeating ping locating whatever you are tracking. It has three fixed notes, " +
                    "not a sliding one: a high note means the target is ahead of you, which is up " +
                    "the screen, which is the way W walks; a much lower note means it is behind " +
                    "you, down the screen; and a middle note means it is off to one side, with left " +
                    "and right carried by the stereo position. None of the notes means distance. " +
                    "Only the ping rate carries distance, speeding up as you close in, so very fast " +
                    "means you are on top of it."
                },
                { "cue.wall_near", "Wall ahead" },
                {
                    "cue.wall_near.detail",
                    "A low pulse when a wall or other solid object lies in the direction you are " +
                    "moving. It speeds up as the obstacle gets closer, and stays silent for walls " +
                    "you are not walking toward."
                },
                { "cue.wall_blocked", "Wall contact" },
                { "cue.wall_blocked.detail", "A broad percussive knock the moment you make contact with solid geometry." },
                { "cue.dodge_direction", "Dodge direction" },
                {
                    "cue.dodge_direction.detail",
                    "Where a dodge is taking you. A soft band of noise, panned and pitched to the " +
                    "direction you are about to travel. Deliberately one of the quietest sounds the " +
                    "mod makes: it reports something that has already happened and there is nothing " +
                    "to react to. It plays only when you dodged with no movement held, so the " +
                    "Lamb's facing decided the direction and you had no way to know it; a dodge you " +
                    "aimed yourself stays silent."
                },
                { "cue.dodge_blocked", "Dodge into a wall" },
                {
                    "cue.dodge_blocked.detail",
                    "The dodge burst followed by a knock, meaning the path the dodge is taking you " +
                    "along runs into solid geometry. Also fires as the dodge starts."
                },
                { "cue.melee_threat", "Melee wind-up" },
                {
                    "cue.melee_threat.detail",
                    "A harsh falling buzz at an enemy's melee wind-up, positioned at the attacker, " +
                    "before its damage becomes real. Deliberately the most aggressive sound in the " +
                    "set alongside the trap: the attacker is already within reach of you, so this " +
                    "is not a sound meant to be pleasant or easy to ignore."
                },
                { "cue.projectile_threat", "Incoming shot" },
                {
                    "cue.projectile_threat.detail",
                    "A harsh double buzz for a hostile projectile whose path is predicted to hit " +
                    "you. A volley produces one warning for the most imminent shot rather than one " +
                    "per shot. It shares its urgency with the other damage warnings on purpose: " +
                    "they all mean move."
                },
                { "cue.area_threat", "Danger area" },
                {
                    "cue.area_threat.detail",
                    "A lower double buzz at a grenade landing area, played when you are predicted " +
                    "to be standing inside it. Unlike the melee and shot warnings it is positioned " +
                    "at your own feet rather than at something across the room, because that is " +
                    "where the danger is."
                },
                { "cue.static_trap", "Static trap" },
                {
                    "cue.static_trap.detail",
                    "A metallic rattle when a trap such as a spike trap has triggered under you. It " +
                    "fires at the start of the trap's own wind-up, about half a second before " +
                    "damage, and it is the loudest cue in the set. The only useful response is to " +
                    "leave."
                },
                { "cue.dodge_avoided_hit", "Dodge avoided a hit" },
                {
                    "cue.evaded.detail",
                    "Two rising notes confirming that a dodge actually rejected an incoming hit. It " +
                    "sounds once per dodge."
                },
                { "cue.timing_window", "Timing window open" },
                {
                    "cue.timing_window.detail",
                    "A steady tone held for exactly as long as a minigame success window is open, " +
                    "such as cooking. Press while you can hear it."
                },
                { "cue.timing_chirp", "Timing edge" },
                {
                    "cue.timing_chirp.detail",
                    "A short chirp as a minigame timing window is entered, for when the sustained " +
                    "tone is switched off."
                },
                { "cue.ambient_wall", "Wall tones" },
                {
                    "cue.ambient_wall.detail",
                    "A continuous ring of quiet tones for the solid geometry around you, in eight " +
                    "directions at once. The tones sustain rather than repeating, which is what " +
                    "makes a doorway findable: walking past a gap in a wall is a sound stopping, " +
                    "not a beat that failed to arrive. Follow a wall, and the opening is the moment " +
                    "that side goes quiet. How much of the direction you hear as a note rather than " +
                    "as a position around you is up to you, under Wall notes."
                },
                { "cue.ambient_item", "Items" },
                { "cue.ambient_item.detail", "Always-on ticks for dropped items and hearts lying near you." },
                { "cue.ambient_interactable", "Interactables" },
                { "cue.ambient_interactable.detail", "Always-on hums for objects near you that you could press Interact on." },
                { "cue.ambient_npc", "Characters" },
                { "cue.ambient_npc.detail", "Always-on tones for followers and other characters near you." },
                { "cue.ambient_enemy", "Enemies" },
                {
                    "cue.ambient_enemy.detail",
                    "Always-on low tones for living hostiles near you. Reaches further and repeats " +
                    "faster than the other categories."
                },
                { "cue.ambient_projectile", "Projectiles in the air" },
                {
                    "cue.ambient_projectile.detail",
                    "Always-on ticks for every hostile projectile in flight near you, for " +
                    "bullet-hell patterns where one warning per shot would arrive too late to help."
                },
                { "config.cue_update", "Update rate" },
                {
                    "config.cue_update.detail",
                    "How often the surrounding geometry is re-measured. The tones themselves never " +
                    "stop and start, so this is responsiveness while walking, not how often a sound " +
                    "plays."
                },
                { "config.sound_folder", "Where to put your own sounds" },
                {
                    "config.sound_folder.detail",
                    "Speak the folder to drop replacement sound files into. Every cue can be " +
                    "replaced by putting a file with its name there; anything you do not replace " +
                    "keeps the built-in sound."
                },
                {
                    "config.sound_folder_is",
                    "Put your own sound files in {0}. Name each file after the cue it replaces, " +
                    "ending in .wav, .ogg or .mp3. Learn sounds says the file name for the cue you " +
                    "are on."
                },
                { "config.sound_file_name", "Replacement file name: {0}" },
                {
                    "cue.dodge_avoided_hit.detail",
                    "Two rising notes meaning a dodge just saved you from damage. Dodging makes the " +
                    "Lamb briefly untouchable, and the game throws away any hit that lands during " +
                    "it; this sounds only when that actually happened, once per dodge. So it " +
                    "answers a different question from the dodge direction cue: that one says where " +
                    "you went as you go, and this one says the dodge was worth it, after the fact. " +
                    "Hearing nothing after a dodge means nothing was going to hit you anyway."
                },
                // Cult-wide bars. All four read high-is-good, because that is how the game
                // itself draws them: a full bar is a healthy cult in every case.
                { "cult.faith", "Faith" },
                { "cult.food", "Food" },
                { "cult.cleanliness", "Cleanliness" },
                { "cult.warmth", "Warmth" },
                { "cult.bar", "{0} {1} percent" },
                { "cult.bar_locked", "{0} locked at {1} percent" },
                { "cult.follower", "{0} follower" },
                { "cult.followers", "{0} followers" },
                { "cult.population_with_dead", "{0}, {1} dead" },
                { "cult.not_started", "The cult has no bars yet." },
                { "cult.unavailable", "Cult status unavailable" },
                { "cult.alert", "{0} low" },
                { "cult.low", "{0} low, {1} percent." },
                { "cult.low_with_consequence", "{0} low, {1} percent. {2}" },
                { "cult.recovered", "{0} back up, {1} percent." },
                { "cult.locked", "{0} is locked and cannot change." },
                { "cult.unlocked", "{0} can change again." },
                {
                    "cult.consequence_faith",
                    "Followers will start to dissent."
                },
                { "cult.consequence_food", "Followers will start to starve." },
                { "cult.consequence_cleanliness", "Followers will start to fall ill." },
                { "cult.faith_rose", "Faith up {0}, now {1} percent" },
                { "cult.faith_fell", "Faith down {0}, now {1} percent" },
                { "cult.faith_locked_no_change", "Faith is locked, so nothing changed" },

                // One follower: the tiles on every picker, and the follower key.
                { "follower.unknown", "Follower details unavailable" },
                { "follower.none", "No follower nearby" },
                { "follower.dead", "dead" },
                { "follower.level", "level {0}" },
                { "follower.loyalty", "loyalty {0} percent" },
                { "follower.food", "food {0} percent" },
                { "follower.health", "health {0} percent" },
                { "follower.pleasure", "pleasure {0} percent" },
                { "follower.unavailable", "unavailable, {0}" },
                { "follower.unavailable_plain", "unavailable" },
                { "follower.needs", "needs {0}" },
                { "follower.doing", "doing {0}" },
                { "follower.trait", "{0} trait" },
                { "follower.traits", "{0} traits" },
                { "follower.disciple", "disciple" },
                { "follower.married_to_you", "married to you" },
                { "follower.spouse", "married to {0}" },
                { "follower.age", "age {0}" },
                { "follower.member_new", "new to the cult" },
                { "follower.member_day", "in the cult {0} day" },
                { "follower.member_days", "in the cult {0} days" },
                { "follower.cult_trait", "cult trait" },
                { "follower.thought_up", "faith up" },
                { "follower.thought_up_lot", "faith up a lot" },
                { "follower.thought_down", "faith down" },
                { "follower.thought_down_lot", "faith down a lot" },

                { "config.cult_status", "Announce cult bar warnings" },
                {
                    "config.cult_status.detail",
                    "Say when the cult's faith, food, cleanliness or warmth drops below a " +
                    "quarter full, and when it climbs back. This is not cosmetic. Below that " +
                    "line the game picks a follower at random and turns them into a dissenter, " +
                    "or makes them starve, or makes them ill, and it warns sighted players by " +
                    "making the bar pulse. It also says when a ritual freezes a bar so it " +
                    "cannot change."
                },
                { "config.cult_in_where_am_i", "Cult warning in where am I" },
                {
                    "config.cult_in_where_am_i.detail",
                    "Add a short reminder of which cult bars are low to the where-am-I key. It " +
                    "adds nothing at all while every bar is healthy, so it usually costs no " +
                    "words. Turn it off to keep that key to your own health and fervour; the " +
                    "cult status key still reads all four bars in full."
                },

                // How a follower is identified in a list: the name first, then the form and
                // level that tell two of them apart across a base.
                { "follower.identity_species_level", "{0} the level {1} {2}" },
                { "follower.identity_species", "{0} the {1}" },
                { "follower.identity_level", "{0}, level {1}" },

                // The one thing worth walking over for. Where the game already has a word for
                // it these are only the fallback, used when the term is untranslated.
                { "follower.headline_catch", "leaving the cult, catch them" },
                { "follower.headline_sin", "sin to absolve" },
                { "follower.headline_quest", "quest to complete" },
                { "follower.headline_reward", "reward to collect" },
                { "follower.headline_attention", "wants you" },

                // A follower who has walked over to ask for something. The game types each of
                // these on the task and then shows one generic bubble for all of them.
                { "follower.complaint_hunger", "is hungry" },
                { "follower.complaint_homeless", "has nowhere to live" },
                { "follower.complaint_sick", "is ill" },
                { "follower.complaint_level_up", "is ready to level up" },
                { "follower.complaint_better_house", "wants a better house" },
                { "follower.complaint_first_meeting", "wants to introduce themselves" },
                { "follower.complaint_grateful", "is grateful" },
                { "follower.complaint_give_quest", "has a quest for you" },
                { "follower.complaint_completed_quest", "has finished a quest" },
                { "follower.complaint_failed_quest", "failed a quest" },
                { "follower.complaint_onboarding", "has something to show you" },
                { "follower.complaint_twitch", "has a message from Twitch chat" },
                { "follower.complaint_speak", "wants to talk" },

                // The speech-bubble icons, which carry no text of their own.
                { "follower.bubble_food", "wants food" },
                { "follower.bubble_home", "wants a home" },
                { "follower.bubble_help", "wants your attention" },
                { "follower.bubble_starving", "is starving" },
                { "follower.bubble_ill", "is ill" },
                { "follower.bubble_sin", "has sin to absolve" },
                { "follower.bubble_ready", "is ready" },
                { "follower.bubble_twitch", "has a message from Twitch chat" },
                { "follower.bubble_dissent", "is spreading dissent" },
                { "follower.bubble_dissent_argue", "is arguing about dissent" },
                { "follower.bubble_meat", "is upset about the meat you served" },
                { "follower.request", "{0} {1}." },
                { "follower.request_located", "{0} {1}. {2}, {3}." },

                { "config.follower_requests", "Announce follower requests" },
                {
                    "config.follower_requests.detail",
                    "Say what a follower wants when they put a speech bubble over their head. " +
                    "That bubble is an icon and a sound with no text in it, so the sound " +
                    "already reaches you and its meaning does not. A follower who has crossed " +
                    "the base to find you is named along with the reason the game recorded, " +
                    "which is more than the bubble itself shows. Only followers inside the " +
                    "scan radius are reported, and each repeats at most once every 45 seconds " +
                    "however long they keep asking."
                },

                // The two cult pages: the Cult tab of the player menu, and the Cult page
                // behind the Doctrine menu. Each number is a bare digit on screen with only a
                // picture beside it, so each has to be named.
                { "cult.named", "{0}." },
                { "cult.home", "{0} home" },
                { "cult.homes", "{0} homes" },
                { "cult.stat_total_followers", "{0} followers ever" },
                { "cult.stat_murders", "{0} murdered" },
                { "cult.stat_starved", "{0} starved to death" },
                { "cult.stat_sacrifices", "{0} sacrificed" },
                { "cult.stat_natural_deaths", "{0} died naturally" },
                { "cult.stat_crusades", "{0} crusades" },
                { "cult.stat_player_deaths", "you have died {0} times" },
                { "cult.stat_kills", "{0} enemies killed" },
                { "cult.stat_winters", "{0} winters survived" },

                { "config.log_follower_wheel", "Log follower command wheels" },
                {
                    "config.log_follower_wheel.detail",
                    "Record what state a follower was in when their command wheel opened, which " +
                    "commands it offered, and which doctrines are unlocked. The game builds a " +
                    "different wheel for a sleeping, drunk, dissenting or zombie follower, so a " +
                    "command you cannot find may be locked or may simply be on another wheel, " +
                    "and the two look identical from the outside. Nothing is spoken."
                },

                { "cult.faith_history_up", "faith up {0}" },
                { "cult.faith_history_down", "faith down {0}" },

                { "autowalk.no_progress", "Autowalk stopped. Not making progress." },
                {
                    "autowalk.blocked",
                    "Autowalk stopped. Scenery to the {0} is in the way; step around it."
                },
                {
                    "autowalk.blocked_plain",
                    "Autowalk stopped. Scenery is in the way; step around it."
                },

                { "route.blocked", "Blocked that way." },

                { "config.wall_notes", "Wall notes" },
                {
                    "config.wall_notes.detail",
                    "How much of a wall tone's direction you hear as a note rather than as a " +
                    "position in the stereo field. All notes gives every screen height its own " +
                    "note. Three notes puts a high note on everything up-screen and a low note on " +
                    "everything down-screen, with one note shared by both sides, so pitch is spent " +
                    "only where panning cannot help. Two notes tells the sides apart from straight " +
                    "ahead and behind. One note leaves direction entirely to panning, which means " +
                    "north and south will sound the same."
                },
                { "config.wall_notes_all", "all notes" },
                { "config.wall_notes_one", "one note" },
                { "config.wall_notes_two", "two notes" },
                { "config.wall_notes_hrtf", "three notes" },
                { "config.melee_reaction", "Melee cue lead" },
                {
                    "config.melee_reaction.detail",
                    "How long before the ideal dodge moment the melee warning sounds, to allow for " +
                    "hearing it and pressing the button. The cue deliberately does not fire when " +
                    "the enemy starts its attack: a wind-up lasts half a second and a dodge only " +
                    "protects you for three tenths, so dodging the moment the attack begins leaves " +
                    "you exposed when it lands. The cue is timed so that dodging when you hear it " +
                    "puts you inside the impact. Raise it if you keep dodging late, lower it if you " +
                    "keep dodging early."
                },
                { "config.wall_ring", "Wall directions" },
                {
                    "config.wall_ring.detail",
                    "How many directions the wall tones listen in. Eight includes the diagonals. " +
                    "Four is north, east, south and west only, which is quieter and can make a " +
                    "doorway sharper: most walls run straight, so a diagonal usually reports the " +
                    "same wall as its neighbour from further away, and hearing it go quiet in three " +
                    "stages instead of one blurs the gap you are listening for. The diagonals are " +
                    "worth more on angled walls and inside corners. Try both against a real wall."
                },
                { "config.wall_ring_four", "four, no diagonals" },
                { "config.wall_ring_eight", "eight, with diagonals" },
                { "config.log_frame_budget", "Log frame timing" },
                {
                    "config.log_frame_budget.detail",
                    "Measure how long the mod's own work takes each frame and record a summary " +
                    "every ten seconds, but only when something took longer than four milliseconds. " +
                    "This turns a feeling that the game is lagging into a number. It cannot see the " +
                    "parts of the mod that run inside the game's own code, so if these numbers stay " +
                    "small while the game still stutters, that is worth knowing too."
                },
            };
    }
}
