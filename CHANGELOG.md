# Changelog

All notable changes to CultAccess. Format follows Keep a Changelog; versioning is semantic
against the mod's own features.

**0.1.0 is the first release, and it is a test build.** It has not been published to
Thunderstore; it is a GitHub release aimed at testers. Parts of it have never been confirmed
by anyone but the author — `TESTING.txt` in the download lists which, and what to send back.

Game version tested against throughout: **Cult of the Lamb 1.5.25.1049** (Steam).

---

## [Unreleased]

### Added

- A second installer button, "Use a BepInEx zip I downloaded myself", for anyone whose
  antivirus blocks the automatic download — which is happening in practice, since a mod
  loader patches a game and that is the same shape as something unwanted doing it. It
  resolves the current download link live from Thunderstore rather than carrying a written-down
  one, copies it to the clipboard, opens it in a browser, then waits and offers a file picker
  for the zip once it has been saved. Falls back to the package page if the lookup fails.

## [0.1.0] — 2026-08-21

### Fixed

- Lumber read out as "Sacred Flame" — a repaired sleeping bag reported costing it, and a
  lumberyard reported generating it. The icon vocabulary is built from the game's own table,
  and the game reuses one sprite for more than one item: `icon_wood` is claimed by both `LOG`
  and `FORGE_FLAME`. Registering in enum order let the second silently overwrite the first.
  A confirmed word is no longer replaced, a second claim on any sprite is refused rather than
  applied, and refused claims are counted and named in the startup log so a clash cannot hide
  again. `icon_Mushroom`, shared by the small and big mushrooms, was the same defect unnoticed.

### Added

**Speech and framework**

- BepInEx plugin with an NVDA controller client speech path, falling back to Tolk and then
  SAPI so the mod is never wholly silent.
- Startup confirmation, `CultAccess ready`, with the resolved speech provider named.
- Inline icon vocabulary read from the game's own table at startup. `FontImageNames`
  maps every item type to the sprite that draws it, and pairing that with the game's
  localised item names gives the complete icon vocabulary in the player's own language,
  replacing six hand-written English guesses.
- `LogSpeech`, logging every line handed to the screen reader after cleaning, and every line
  dropped as a repeat. The only diagnostic that can answer "what did I actually hear?" —
  announcers log from their own side in their own wording, and most do not log at all.
- Runtime-toggleable structured diagnostics written to the BepInEx log, never spoken.
- `LogFocus`, a per-focus diagnostic naming the adapter that described each control, what
  was spoken, and the surrounding component chain. Menus were the only announcement path
  with no log evidence at all, so a screen that read badly and a screen never opened were
  indistinguishable afterwards; `adapter=generic` now names every control still read by
  fallback.
- Help on F1, one section per press; read panel on F8; speech diagnostics on F9; silence on
  F10; repeat last on F11; where-am-I on F12.

**Menus and UI**

- Why a building placement was refused. `IsValidPlacement` returns a bare bool and the readout
  could only repeat it: a live session shows sixteen cells refused in a row, each described as
  "empty, invalid placement", with nothing to search on. The check walks the structure's whole
  footprint rather than the cell under the cursor, so the anchor really can be empty while a
  neighbour is what refuses — the refusal now names the offending cell's direction and cause,
  such as "a path runs through, 2 north" or "Sleeping Bag in the way, 1 east".
- A key, F5, that says which way the nearest cell is that the structure would actually fit in.
  It reports a bearing and distance rather than moving the cursor.

- The world map's destinations. The map is a picture with icons and no text beside them, so
  it announced nothing at all on a screen whose whole purpose is choosing where to go. Each
  location now gives its name, whether it is available, completed, locked or not yet
  revealed, and its position.

- The follower indoctrination pickers: form, variant, colour, outfit, necklace and traits.
  A form tile is a rendered character with no text at all, so it had been reading as the bare
  word "button", and a trait tile read as its own GameObject name. Forms now speak their
  authored title, whether they are locked, and which biome they come from; traits speak their
  localised title and effect. The same pickers feed `TwitchVoting`, so an unlabelled tile meant
  knowing neither what you were choosing nor what chat chose for you.

- Twitch Help or Hinder: the vote starting, and what chat chose, read from the game's own
  localised name for the effect. The base gate is also announced as held shut while a vote
  runs — it locks for about 35 seconds, and the target had been reading as unavailable with no
  reason, which is worse than silence when waiting is the only correct response.

- Re-education progress: how much dissent is left and how many more daily sessions a
  dissenter needs. The game delivers this entirely as an invisible number, so seven sessions
  spread one apiece across seven followers left every one of them uncured and sounded exactly
  like nothing happening.

- The Player Upgrades window behind the Temple Altar's Crown option: crown abilities and
  fleeces now read their name, whether they are owned, affordable or unaffordable, their cost
  against what you hold, their description and their position. Neither tile type carries a
  name of its own — the only text on either is the price — so the generic reader had been
  announcing the cost as though it were the item.
- Sermon results: the doctrine category the sermon fed, the experience gained, progress
  toward the next upgrade, and how many followers attended. All of it was previously
  delivered through a bar and a follower overlay with no sound at all.

- Focus reading for labels, roles, states and positions across menus, including sliders,
  horizontal selectors and toggles, which required dedicated before/after value comparison
  because they keep focus while changing.
- Settings tab surface with live Rewired page-left and page-right bindings announced.
- Multi-page tutorial overlays, with page X of Y, live controls, and F8 to repeat.
- Inventory, player details, quest log, rituals, doctrine choices and history, and both
  upgrade trees.
- Radial wheels: follower work, doctrine category, weapon and curse, each with the required
  hold-direction-plus-E gesture.
- Adventure map nodes with type, availability, cost and choice position; hidden nodes stay
  hidden.
- Death and run-results screen, announced once the values have finished animating.
- Twitch connection state, raffles and follower votes.

**World navigation**

- On-demand world scanning with ten target categories, cycled with the slash key.
- Continuous spoken route guidance with go, turn, continue and final instructions over the
  game's own A* graph.
- Audio beacon through FMOD, with pitch encoding vertical aim and ping rate encoding
  distance.
- Enemy roster and enemy beacon lock.
- Pending routes that retry rather than declaring a target permanently unreachable, with
  named destructible blockers where the game verifiably updates its graph.
- Dedicated adapters for base goop passages, base dungeon entrances, weapon podiums and
  heart pickups.

**Base building**

- Build catalogue entries with availability, exact reason, costs and owned quantities.
- Live placement cursor reporting direction, grid coordinates, contents, footprint, rotation
  and validity from the game's own placement check.
- Catalogue refresh when a build site is placed and again when the finished structure exists.

**Status and progression**

- Health, fervour and tarot state, settled so a burst reports one accurate result.
- Named automatic heart pickups correlated with the resulting health total.
- Objective added, updated, completed and failed, settled for 0.35 seconds.
- Transient notification cards including signed item deltas and running totals.

**Settings menu**

- A spoken, keyboard-driven settings menu on **F2**, drawing nothing on screen. Up and down
  move, left and right change a value, Enter opens a section or runs an action, Backspace
  goes back and closes from the top, Home and End jump to the ends. The game is deliberately
  not paused while it is open.
- Every setting in it is the same setting as the matching line in the config file: the menu
  writes through the BepInEx entries, so a change made in game persists and a change made in
  the file appears in the menu.
- Sections for speech, sounds, wayfinding, learn sounds and diagnostics. A cue switched on
  but silenced by a section or master switch says so, rather than appearing to work.
- **Learn sounds**: moving onto a row speaks the cue's name and then plays it, and Enter
  plays it as a sweep — left, centre, right, then low, level and high — which demonstrates
  that pitch carries vertical aim rather than distance.

**Sound configuration**

- A switch and a volume for every individual cue, plus a section switch per group and an
  overall volume. Generated from one cue list, so a new cue cannot exist without being
  configurable and teachable; a startup check reports by name if one is ever left out.
- Guidance mode: beacon and speech, beacon only, or speech only. It governs only the
  automatic channels — refusals, arrivals and on-demand direction requests always speak.
- Turning a combat cue off now also stops the detection work behind it, so an unwanted cue
  costs nothing rather than being computed and discarded.

**Replaceable sounds**

- Every cue can be replaced by dropping a file named after it into `sounds/` beside the
  plugin. `.wav`, `.ogg` and `.mp3`; no rebuild and no setting to change. The generated
  waveform remains the guaranteed default behind it, the same two-layer shape the string
  catalogue uses. Nothing ships in that folder, so there is no third-party audio to license.
- Learn sounds says the replacement file name for whichever cue is focused, and
  `sounds/README.txt` lists them all with notes on looping and levels.
- A file that is present but cannot be loaded is named in the log and falls back to the
  built-in sound, rather than leaving the player convinced their file is in use.

**Always-on sounds**

- A continuous proximity layer for walls, items, interactables, characters, enemies and
  hostile projectiles, **off by default** and enabled one category at a time.
- Volume falls off steeply rather than linearly, so a source is effectively inaudible until
  it is close. This is what makes wall tones usable for following a wall and finding the gap
  in one, instead of filling the room.
- Wall tones are a **sustained** ring of eight directions, each with its own note as well as
  its own stereo position: higher means further up-screen, matching the beacon, so north is
  the top note and south the bottom one while east and west differ only in pan. The notes are
  a pentatonic set, so directions sounding together form a chord rather than a clash.
  Walking past a doorway is a tone **stopping**, which is immediate; with the pulsed version
  it was a beat that failed to arrive, which is not. Distinct from the combat wall cue, which
  warns only about the direction the player is already moving.
- Wall tones are exempt from the simultaneous-source cap and the per-second budget. A
  direction culled to reduce clutter is silent, and silent is how the player is told they can
  walk that way, so capping them would invent doorways.
- Per-category range, repeat rate and simultaneous-source cap, plus a ceiling on the total
  across all categories. When a crowded room reaches it, incoming fire keeps sounding and
  scenery does not.
- Built entirely on the game's own registries — `Health` team lists, `Projectile.Projectiles`,
  `PickUp.PickUps`, `Interaction.interactions`, `Follower.Followers` — so the layer never
  walks the hierarchy despite running several times a second.

**Combat cues**

- Static-trap warning: a metallic rattle at the moment a spike trap commits, which is the
  start of its own half-second wind-up and before any damage. Loudest cue in the set,
  because there is nothing to face, nowhere to dodge to, and no second chance to notice it.
- The melee wind-up is now a harsh falling buzz built from a dense saw-like harmonic stack
  with a held level and a fast tremolo, replacing a smooth sine descent that was reported as
  far too quiet and unassuming for what it means. That was a fault of material rather than
  level: a pure tone reads as an announcement however loud it is made.
- Wall approach and contact cues from a collider-accurate forward cast.
- Dodge cues: silent on a clearly directed roll, a direction chirp only when facing decides
  an otherwise unknown direction, chirp plus knock when the corridor is blocked.
- Predictive projectile warnings based on relative closest approach inside a configurable
  horizon, collapsing volleys to the most imminent threat.
- Grenade warnings from the game's own landing target and travel time.
- A melee wind-up warning for the base scuttle swiper.
- Evade confirmation only when a hit was genuinely rejected because the player was dodging.
- Combat start and an authoritative room-clear that cannot fire between waves.

**Minigames**

- Cooking and tailor menus, with mode, live interact key, and a rising timing cue in manual
  mode that supplies no input.

### Fixed

- Speaker name lost on every follower conversation in the game. The mod reduced a
  conversation entry's `CharacterName` to its last slash-separated segment, which is right
  for a term path such as `NAMES/CultLeaders/Dungeon2` but wrong for the nine sites where
  the game assigns a literal name wrapped in `<color=yellow>...</color>` — every
  `interaction_FollowerInteraction` path, `FollowerRecruit` and the Bank. The last slash was
  the one inside the closing tag, so the name resolved to the literal string `color>`, which
  the markup guard then discarded. Markup is now stripped before the path is split, and
  translated names are cleaned rather than spoken raw.
- Inventory entries in the item selector overlay — the one a trade, an offering or a feeding
  puts up — read by the generic fallback. The adapter required a `GenericInventoryItem` to
  sit under an `InventoryMenu`, and the game reuses the entry component outside it. The
  overlay's entries now read their name, quantity and choice position, and the "Read only"
  suffix is confined to the real inventory menu, where it is true.
- Walking directions cut off world events. They were spoken at interrupting priority, and the
  provider's cancel is all-or-nothing, so a direction could destroy a queued announcement —
  a follower dying, a building finishing — that had no second chance. Directions now supersede
  only each other: rapid turns still replace one another rather than piling up, and nothing
  else is lost. Losing a direction costs nothing, because another follows within a second.
- Stepping through the target list read the position before the name — "2 of 38, Shrine" —
  so every entry began identically and the only part that differed came last. It now reads
  "Shrine, ..., 2 of 38".
- Icon-font characters read out as garbage. The game bakes glyphs from the Unicode Private
  Use Area straight into label text — `FontImageNames.IconForCommand` alone returns 49 of
  them — and they carry no meaning outside the font that draws them. They are now removed,
  replaced by a space so an icon between two words cannot fuse them together. Characters
  just outside that range, such as the fullwidth digits the game also uses, are real text
  and are kept.
- Ritual tiles in the Player Upgrades menu, reached through the altar's Crown option, read
  as their price — "Doctrine Stone 1, button" and "On Cooldown, button" — instead of as
  rituals. A working adapter already existed but was gated on the tile having a rituals-menu
  controller above it, and the game reuses `RitualItem` outside that menu.
- Projectile warnings firing far faster than their own rate limit whenever several
  projectiles were live, heard as bullet hell in a room with eight arrows in flight. The
  monitor reduces every live projectile to one warning on a 0.14 to 0.48 second interval
  scaled by urgency, but a change of source could bypass that interval and re-fire after
  0.08 seconds. With many projectiles the identity of the most imminent one churns every
  few frames — in one miniboss room the source changed on 15 of 20 consecutive cues — so
  the exception governed instead of the rule. The interval is now the whole rate limit, and
  the cue pans to whichever threat is most imminent when it fires.
- Every resource pickup burst announced twice — the first item alone, then the remainder as
  a second sentence, heard as "Gained 1 Coin, 17 total" immediately followed by "Gained 2
  Coin, 19 total". The notification card's `Configure` call spoke at once while `UpdateDelta`
  aggregated the rest behind a settle timer; both now feed the same pending change.
- Startup abort when Harmony was pointed at an inherited method rather than its declaring
  type. Happened twice, once for `UIRadialWheelItem.DoDeselected` and once for
  `UIInventoryController.Close`; each disabled every mod hotkey before startup completed.
  Patch installation is now guarded so one mismatched optional adapter cannot abort `Awake`.
- Route waypoints held at already-reached nodes because distance included the graph-versus-
  render depth difference. Route decisions now use planar X/Y distance and project waypoints
  to the Lamb's live depth.
- Guidance to an open dungeon entrance failing on a fresh load, because the transition
  trigger deliberately sits beyond the room's walkable graph. Path endpoint and physical aim
  are now separate.
- Guidance aiming at the centre of a base goop wall instead of the passage through it.
- Rapid-fire navigation instructions when a path was exhausted without the target becoming
  the current interaction.
- Dungeon doors with follower thresholds reported as actionable; the current and required
  counts were also spoken in the wrong order.
- Weapon podiums reported unavailable because their label is assigned only at close range.
- Heart pickups spoken as `dot`, their label being an icon placeholder.
- Bulk resource nodes flooding the Actions Now and Everything categories.
- Objective spam during multi-item harvests.
- Untranslated localization terms spoken as slash-paths.
- Dodge direction chirp firing on every roll, where it added nothing and could be mistaken
  for a success confirmation.
- Combat cue waveforms that were detected correctly but could not be perceived over the
  game's mix.
- **The dodge cues and the evade confirmation were impossible to tell apart, and it was not
  only the sound.** They are now named for what each reports rather than as two synonyms:
  `DodgeDirection` fires as a dodge begins and says where it is taking you, and
  `DodgeAvoidedHit` (previously `Evaded`) fires afterwards and says the dodge was worth
  making. Their explanations state the mechanic behind the second one — dodging puts the Lamb
  in a state where `Health.DealDamage` refuses outright — so hearing nothing after a dodge is
  legible as "nothing was going to hit you" rather than as a missing cue.
- **The dodge cue and the evade confirmation were nearly the same sound.** One was a 650 to
  1350 Hz rise and the other a 700 to 1450 Hz rise, in the same register with the same
  envelope and harmonics, and they were reported as indistinguishable. The dodge cue is now a
  filtered noise burst with a hard transient — the only cue in the set built from noise — and
  the evade confirmation is two discrete rising notes rather than a glide, so the two differ
  in material and in rhythm, not only in pitch. The offline harness now measures the
  correlation of every pair of cues and fails if two converge again.
- Damage diagnostics reporting that no warning had been given even when a melee wind-up cue
  had just played, which made the field useless for finding attacks that still need a cue.

**Localisation**

- Mod speech now comes from an editable catalogue rather than being baked into the code.
  `lang/en.txt` ships beside the plugin; copy it to `lang/<code>.txt` and translate the
  right-hand side to add a language, no rebuild required. The mod follows the game's own
  language setting, and any line you leave out falls back to English, so a partial
  translation works fine. Item and place names still come from the game's own translations
  so they match the rest of the interface.
- A translation can record whether it was machine-drafted or reviewed by a native speaker,
  and the mod reports at startup how many lines your language actually covers, so an
  unfinished translation is visible rather than silently falling back.

**Progression and followers**

- The cult tutorial's next step is announced when it changes and reported by the where-am-I
  key. This reads the game's own onboarding phase, so it stays correct even when the target
  list has gone quiet. Loading a save never narrates it.
- Followers are named from across the room instead of only when standing next to them, and
  the followers filter now lists them.
- Recruiting or indoctrinating a follower refreshes the target list on its own, so a gifted
  follower appears and stale build blockers clear without a manual rescan.
- A saved recruit waiting at the base no longer disappears from navigation before its world
  object has spawned. Actions Now and Story and Quests expose `Indoctrinate new follower` at
  the game's authored indoctrination platform, which triggers the real interaction on approach.
- The Shrine onboarding status no longer keeps saying `build a Shrine` after construction when
  a saved recruit is waiting. The following Devotion step now names both commanding a follower
  to worship and collecting Devotion.

- During a story beat the game disables every interaction in the world. The target list now
  says a story moment is in progress and repeats what the game is waiting for, instead of an
  unexplained empty list that read as a stuck save. Individual targets say "not usable during
  this story moment".
- A marker key (F7 by default) stamps the log with your position, the game's location and the
  current objective step, for reporting anything the mod failed to announce.
- The Temple altar is listed under Facilities, and doorways back to the cult are announced as
  "cult grounds" rather than an internal name.
- Cooking's timing chirp now sounds slightly ahead of the success window, and a sustained
  tone holds for exactly as long as pressing would succeed, so you can hear how much time
  you have rather than react to a single instant.
- Cooking Fire recipe buttons announce name, availability, the exact ingredients with the
  amounts you own, hunger stars and position. Undiscovered recipes stay unnamed.
- Each new in-game day is announced along with what it restores, such as the Sermon, and
  nightfall is called out. The other phase changes are deliberately silent.
- Chore level-ups are announced — the progression that upgrades the Lamb's mop, which the
  game shows only as a bar and an animation.
- A garbled speaker name is no longer read aloud on dialogue lines where the game's markup
  survives cleanup.

### Known issues
- Guidance to a script-played conversation can aim tens of units from where the conversation
  actually happens, because the mod trusts the interaction's activation offset even for
  interactions the game triggers itself and never range-checks.
- An already-spoken conversation stays in the target list permanently as an unnamed,
  permanently unavailable Story entry.
- The localisation migration is partial. The layer exists and the day cycle, onboarding,
  building entrance and dungeon door wording use it; most other announcements are still
  hardcoded English.
- No controller support, and the game's controller bindings have not been dumped.
