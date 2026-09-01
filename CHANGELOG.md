# Changelog

All notable changes to CultAccess. Format follows Keep a Changelog; versioning is semantic
against the mod's own features.

**0.1.0 is the first release, and it is a test build.** It has not been published to
Thunderstore; it is a GitHub release aimed at testers. Parts of it have never been confirmed
by anyone but the author — `TESTING.txt` in the download lists which, and what to send back.

Game version tested against throughout: **Cult of the Lamb 1.5.25.1049** (Steam).

---

## [0.2.2] — 2026-08-28

A followers and cult-stats pass, plus two sessions of listening folded back in.
Packaged as `CultAccess-tester-0.2.2.zip`.

### Added

- **The cult's four HUD bars, on F3.** Faith, food, cleanliness and warmth, plus the living
  and dead follower counts. The game draws all four as `Image.fillAmount` with a threshold
  colour and no text anywhere, and reveals each behind its own `DataManager.ShowCult*` flag,
  so a bar the player has not unlocked is left out rather than read as zero.

  All four are stated high-is-good, because that is how the game orients them: faith is
  `CurrentFaith / 85`, food is the mean of satiation minus starvation, cleanliness is
  `1 - waste / worst-ever-waste`, warmth is furnace fuel. There is no localised label for any
  of them anywhere in the game — the bars are icons — so the names are the mod's own, with the
  community's own words for them given in the help text.

- **A crossing warning when a bar drops below a quarter, and when it climbs back.** This is
  the half that matters. `CultFaithManager.UpdateSimulation`, `HungerBar.UpdateSimulation` and
  `IllnessBar.UpdateSimulation` each pick a random available follower below that same fraction
  and make them a dissenter, starving, or ill, at most one per 3600 game-seconds — and the
  game tells sighted players by punch-scaling and pulsing the bar. So the mod names the
  consequence with the crossing: "Faith low, 20 percent. Followers will start to dissent."

  Warmth deliberately claims nothing. Its own `UpdateSimulation` is a no-op and its pulse
  additionally requires the bar to be rising, so it warns of nothing; asserting otherwise
  would be inventing a mechanic.

- **A short "faith low" clause on the where-am-I key**, and nothing at all while every bar is
  healthy. The full four-bar reading has its own key precisely because that one is already
  five clauses and is pressed constantly. `Speech / AnnounceCultInWhereAmI` turns it off.

- **Faith notifications now carry their number.** `NotificationFaith` puts the reason in
  `_description` and the amount in `_faithDeltaText`, and only the first was being read — so a
  follower dying arrived without the ten points of faith that went with it. One event, one
  sentence, naming both and the level it left the cult at. Zero-delta cards, which the game
  uses for purely informational notices, add nothing.

- **Every follower tile in the game now reads its bars, on F4's data path.**
  `FollowerInformationBox` is what the roster, the sacrifice picker, daycare assignment,
  mating, beds, the healing bay, knucklebones and the confession booth all instantiate, so one
  adapter covers the lot. Loyalty, food, illness and pleasure are `fillAmount` with no text
  beside them; the mod reads the card's own fills and its own container flags rather than
  re-deriving the rules, so what is spoken is what is drawn.

  Tiredness is computed by the same card and then hidden unconditionally, so it is absent
  here too. Loyalty is hidden for a mutated follower and food and illness for a dead one, and
  both are honoured.

- **The 38 reasons a follower cannot be chosen.** `FollowerSelectEntry.Status` has a localised
  string per value — dissenting, imprisoned, too many traits, already married — which the card
  resolves into `_unavailableText`. Now spoken directly after the name, because it is what the
  player is deciding on.

- **A per-follower readout on F4.** Loyalty, food and health, what is wrong with them, what
  they are doing, traits, marriage, age and time in the cult. Describes the follower selected
  in the target list, falling back to the nearest one. The "what is wrong" line is the game's
  own: the summary screen synthesises a `BiggestNeed_*` thought — exhausted, homeless, broken
  bed — and that is reproduced rather than invented, minus the branches that would only
  restate a condition already named.

  Task names are humanised from `FollowerTaskType`. There is no localisation key for any of
  the 137 values, because the game conveys the task through animation and position only.

### Added, second tranche — the follower push channel

- **Followers in the target list now carry what a sighted player reads off them.**
  `Sinterklaas the level 4 Goat, ill, quest to complete`, before the usual distance and bearing.

  The form is the game's own authored title from `WorshipperData`, and it leads over the name
  for a reason found while checking this: `SettingsManager.Settings.Game.ShowFollowerNames` has
  no initializer, so **overhead name plates are off by default**. Form, not name, is what
  identifies a follower across a base for everyone.

  The last clause reproduces `interaction_FollowerInteraction.GetLabel`'s own ranking — protect,
  catch a spy leaving, absolve sin, complete a quest, collect a levelling reward — derived from
  the fields that label branches on rather than from the label, which is empty at any distance
  and outside the base. The game's own words are used where the term is a bare label; only sin
  needed mod wording, because the game's string is a whole sentence with the name inside it.
  A follower running `GetPlayerAttention` fills the clause when nothing else applies, so the
  game's ranking always wins where the two overlap.

  Computed live per announcement rather than baked into the scan: all three parts change without
  the catalogue being rebuilt. Low verbosity is unchanged, still `Sinterklaas, follower`.

- **Speech bubbles are read.** `WorshipperBubble.Play` shows one of twenty-one icons for four
  seconds and plays `event:/followers/speech_bubble` positioned at the follower — so the sound
  was already reaching the player and none of its meaning was. Announcing that something was
  said and withholding what is worse than silence.

- **A follower who crosses the base to find you says why.** `FollowerTask_GetAttention` carries
  a public `ComplaintType` — hungry, homeless, ill, ready to level up, wants a better house,
  first meeting, grateful, has a quest, finished a quest, failed a quest, tutorial, Twitch
  message, wants to talk — while showing the same generic help bubble for all thirteen. The task
  is the only place that distinction survives, so it wins over the icon.

  Hooking the bubble rather than the task puts the pacing in the game's hands: it re-bubbles
  every four to six seconds indefinitely, and a per-follower cooldown of 45 seconds turns that
  into one line until the reason changes or the player acts. Gated on the scan radius.

  `LOVE`, `ENEMIES`, `FRIENDS` and the four `BOSSCROWN` variants stay silent. Two followers
  gossiping or one admiring the Crown changes no decision, and a cue that conveys nothing new is
  noise even when it is accurate.

### Corrected

- **The overhead loyalty bar is not an at-a-glance property**, which the previous entry implied.
  `FollowerAdorationUI.Show()` has three callers — the follower interaction, the ranch, and
  onboarding — and `ShowAllFollowerIcons` defaults `excludeLoyaltyBar` to true. Loyalty is an
  interaction-time reading. `FollowerPleasureUI` is shown only by `RitualAtone`, and
  `FollowerXPProgressBar` has no external callers at all.

### Added, third tranche — the cult pages and a wheel probe

- **The Temple altar's Cult page reads its statistics.** Nine numbers — followers ever,
  murdered, starved, sacrificed, died naturally, crusades, your own deaths, enemies killed,
  winters survived — each labelled on screen only by the picture beside it. **Only the two
  buttons on that page are `Selectable`**, so focus could not reach the numbers even to read
  them badly, and the generic panel collector would have recited a run of digits. Named
  individually and read on the panel key.

- **The Cult tab of the player menu reads too**: cult name, all four bars, population and homes.
  Its notification history is deliberately left to focus, whose rows each carry their own
  `Selectable` — folding it in would have made one keypress recite the whole history of the save.

  Both take their values from `DataManager` rather than from the labels. Simpler than reflecting
  nine private fields, and it cannot pick up a stale or half-animated string.

- **A log-only probe on the follower command wheel**, answering a question open since before
  this pass: when a command is not on a follower's wheel, is it locked or simply on a different
  wheel? `FollowerCommandGroups.DefaultCommands` dispatches over follower state — snowman,
  hibernating, child, zombie, asleep, drunk, dissenting — each branch returning a wholly
  different list, so the two causes look identical from outside.

  Rather than re-implementing that dispatcher and letting it drift, the probe records the fields
  it branches on, the list it actually produced with per-entry availability, and the unlock flags
  for the five doctrine-gated commands. Grep `[follower wheel state]`. Never speaks: the wheel is
  a modal the player is mid-decision in.

### Not added, and why

- **A follower state-change feed.** `MakeSick`, `MakeStarve`, `MakeDissenter` and
  `ApplyCurseState` all route through `NotificationCentre.PlayFollowerNotification`, and
  `NotificationFollower` derives from `NotificationBase` — the class `NotificationAnnouncer`
  already patches. "A follower has become a dissenter", "is starving" and "is ill" are therefore
  **already spoken today**. Building the feed would have duplicated an existing announcement
  rather than filled a gap, which is worse than the gap.

- **Fishing cues.** Scoped and deliberately deferred rather than folded in. Only half of it is
  the shape it was assumed to be: `UIFishingOverlayController` is a two-phase minigame, and
  while the casting phase is a timed press that `UI/MinigameTimingCues` already covers, the
  reeling phase drives a `targetSection` around a bar under a `Difficulty` carrying section
  gaps, move speed, range and a random timer. Keeping a marker inside a moving window is a
  continuous tracking problem — nearer the beacon than a timing cue — and needs its own design.
  Scoping it as "the same as cooking" would have delivered half a feature.

### Fixed from the first session log, 2026-08-25

- **Auto-reading a panel never consulted `CultPageDescriber`.** `PanelReader.Tick` goes straight
  to the generic collector; only the on-demand key walks the describer chain. The player reached
  the Cult page and never pressed the panel key there, which is the right instinct — nothing
  about that screen suggests one. Auto-read now tries that one adapter first.
- **A skin identifier reached the player as a species**: `gozer the DeerRitual`.
  `WorshipperData` titles are a mixture of real names and run-together identifiers, so a
  one-word title with an internal capital is now split. The same defect exists in the form
  picker, which has shipped for weeks.
- **Double full stops** where the game's own description already ended in one, or in an
  ellipsis: `Your flock grows....`, `You gave a Sermon..`, `our glorious Leader., faith up a lot`.
- **Notification history rows led with a bare faith number** — `5, You have a new Follower` —
  unlabelled and identical as the first word of every row. Moved behind the sentence and named.
- **The thought arrow now logs the sprite it matched.** Every row seen in the first session
  reported "faith up a lot", which is either correct or a mismatch, and the outcome cannot tell
  them apart. Instrumented rather than guessed at.

### Fixed

- **Cult traits and character traits read identically.** The follower summary screen builds two
  grids from the same `IndoctrinationTraitItem` under headings a screen reader never reaches,
  so a cult-wide trait was indistinguishable from one belonging to the follower in front of
  you — a wrong answer rather than a missing one. Cult traits now say so.

- **A thought's faith arrow is now described.** The summary screen's thought rows carry name
  and description as real text, but how much faith each is worth exists only as one of four
  arrow sprites. Read from the row's own icon and named as the arrow — "faith down a lot" —
  rather than as the underlying modifier, which the card does not show to anyone.

### Added, fourth tranche — what the second session's log asked for

- **Dropped items are walkable targets.** A Flower Necklace on a dungeon floor played the
  ambient item cue the whole way in and never appeared in the target list: the soundscape reads
  `PickUp.PickUps` and nothing in navigation referenced that registry at all. The mod was
  beeping about something it offered no way to reach, which is a worse shape than saying
  nothing. Named from the game's own `InventoryItem.LocalizedName`, with the stack size where
  it is more than one — `Flower Necklace`, `Berry, 4`. Hearts and loot chests already arrive as
  interactions and the existing duplicate guard keeps them from being listed twice.

- **Guidance and the obstacle sonar now agree.** Walking directions were still saying "one
  metre west" while the blocked cue was playing in that same direction, because the two systems
  had no channel between them. The sonar's last contact is now exposed for 0.4 seconds, and a
  step whose heading falls inside a sixty-degree cone of it ends `"Blocked that way."` Both the
  stepped and the direct-line forms carry it.

- **A route to somewhere genuinely unreachable says so.** In Pilgrim's Passage the Fisherman and
  the fishing spot sat on A* area 54 while the player stood on area 52 — both walkable, no
  blockers, two connected components that no amount of waiting joins. "No current route, I will
  keep checking" was a promise the mod could not keep, and the player stood waiting for
  something that could not arrive. A `Disconnected` route now says there is no walkable route
  from here, that it is not connected to where you are standing, and to try another way in.

- **The log marker records the followers around you.** F7 now stamps every follower inside the
  scan radius with `task`, `cursed`, `role`, `drunk`, `rest`, `illness` and `injured`. A
  follower's body animation *is* its task, so the list identifies what a cluster of unexplained
  movement nearby actually was — which is the question a marker gets pressed for.

### Fixed from the second session log, 2026-08-26

- **A stray pipe reached the player thirty-five times.** `"Re-Assign |, gozer Lives Here"`,
  `"Role: Devout Worker | Age: 20 Days"`. The game uses a pipe as a visual separator between two
  facts and leaves one dangling where a stripped icon used to be; a screen reader says "vertical
  bar" for both. `RichText.Clean` turns it into a comma before the existing run-collapsing, so
  dangling ones vanish and genuine separations become pauses.

- **The follower summary screen read the prefab's placeholder text** — `"Follower Name, Married
  Spouse"`. On that prefab the `FollowerInformationBox` hangs *below* the focused button rather
  than above it, so the parent search missed it; the roster nests the other way round, which is
  why the same adapter worked there and only there. Both directions are searched now, and the
  log says which one resolved.

- **Autowalk drove the straight line to the target whenever the route had no waypoint.** The one
  stall in that session was 34 metres out with no waypoint, driving at a room wall — the exact
  thing following a route prevents. That fallback is only defensible for the short off-graph hop
  at the end, where guidance itself switches to a direct line, so it is gated on that now.
  Everywhere else no waypoint means no route yet, and inventing one is autowalk deciding
  something.

- **A stalled autowalk names what stopped it** instead of only reporting no progress:
  "Autowalk stopped. Scenery to the north east is in the way; step around it."

- **A recovery was announced for a fall the player never heard.** Food crossed low away from the
  base, where the crossing warning is suppressed, and the climb back over the line was then
  spoken on its own — so the player was told food was back up without ever being told it was
  down. A recovery is only news to someone who heard the warning.

- **`"visionlessCult., 7 followers ever"`** — the cult name template carries its own full stop,
  which suits the Cult tab's space-joined reading and not the statistics list's comma-joined
  one.

### Diagnostics

- `[cult status]` — a baseline line carrying all four bars at once, one per requested readout,
  and one per crossing. A crossing that happens away from the base is recorded as
  `suppressed=not-at-base` with the sentence it would have said, so the log answers whether
  the base-only gate is losing anything rather than hiding the question.
- `[follower card]`, `[follower thought]`, `[follower readout]` — what was read, which bars
  were shown or hidden, and the resulting sentence.
- `[indoctrination]` trait lines gained a `cult=` field.
- `[nav instruction]` gained `blocked=`, so a direction spoken while the sonar was in contact
  is visible in the log whether or not the clause fired.
- `[mark]` gained one line per follower inside the scan radius, and `followers=none within N
  units` where there were none.
- `[cult status]` gained `suppressed=unannounced-fall` for a recovery whose fall was never
  spoken.
- `[silent sequence]` gained `framingDropped=` and `duplicatesDropped=`, so the probe's own
  noise is counted rather than printed.

### Notes

- `FollowerCardDescriber` deliberately claims only `FollowerInformationBox`, not its base
  `FollowerSelectItem`. The dead-follower box, the missionary item, the demon item and the
  Twitch box carry their own prose that the generic reader already picks up; claiming them
  would have replaced working text with a shorter reading. The `[focus] adapter=` field will
  say if any of them needs one.
- The thought arrow is read through five reflected fields rather than a Harmony patch on
  `FollowerThoughtItem.Configure`. A renamed field costs one description; a patch target that
  no longer resolves aborts `PatchAll` and silences the whole mod.

---

## [0.2.1] — 2026-08-25

Part of a combat awareness pass, plus a sweep for progress that carries no text.

### Fixed

- **The target list went stale for most of a run, and my previous fix was measuring the wrong
  thing.** It hooked room changes to A\* graph replacement. A 56-room session produced **7 graph
  instances and 5 `graph-replaced` events** — the game reuses one `AstarPath` across a whole
  floor, so the signal fires at biome level and nothing finer.

  Symptoms, all reported from play and all the same cause: `everything scanned=3 shown=0` with
  every filter empty in turn, and pressing guide on the nearest enemy answering
  **"That target is gone"** because the list held a corpse from a room already left.

  Now hooked to `BiomeGenerator.Instance.CurrentRoom`, the game's own answer to which room this
  is — a reference compare per frame, changing exactly once per room.

- **A contradiction now heals itself instead of being announced.** Everything holding nothing
  while the scan holds something means every scanned entry is dead, which only happens when the
  scan describes somewhere the player has left. That forces a re-scan rather than saying
  "nothing nearby". Rate-limited to once per five seconds, because `Refresh` re-applies the
  category on its way out and would otherwise loop on a genuinely empty room at 57 ms a time.

  Predicting every way a room can end clearly does not work; detecting that the answer is
  impossible does.

- **"That target is gone" is no longer a dead end.** It re-scans and hands back a live list,
  which is what stepping the list already did. Refusing left the player holding the same stale
  list to try again from.

- **The temple boss door was announced as "currently unavailable" — a door you walk through.**
  `Interaction_TempleBossDoor` is an `Interaction` nobody interacts with: `OnTriggerEnter2D` on
  the Player tag changes room, `Interactable` is false for almost its whole life, and every
  label slot is empty, so the generic label path drew exactly the wrong conclusion. Principle 3,
  again — a UI-facing availability read as the truth.

  It is now an automatic passage like the base gates and open dungeon doors, keyed on its own
  private `Unlocked` field: **"Temple Boss Door, walk through without pressing Interact"** when
  open and **"Temple Boss Door, sealed"** when not, Locked rather than Unavailable because it
  opens later in the same run. Fails to *sealed* if the field cannot be read, since inviting
  someone to walk into a shut door is the worse mistake.

  Worth separating from `EndOfRunDoor`, which handles the end of a **route** — a `MiniBossFloor`
  node. The end of a **run** is this door. Two mechanisms that had been conflated under one name.

### Added

- **The world map reveal is announced.** `UnlockMapLocation.Play` discovers a location, opens
  the world map on it and parts clouds over the icon, then closes itself. It is a cutscene, not
  a menu — nothing focused, no list, the only text riding an animation — so the generic reader
  found nothing and the player heard the window title, "Cult of the Lamb", and then silence. A
  whole region opening up, unannounced.

  Now: **"New location revealed on the world map: Pilgrim's Passage."** Named from
  `WorldMapIcon.GetLocalisedLocation()`, matched the way the controller finds its own target
  icon, so it is the game's own wording in the player's own language. A re-reveal gets a
  different sentence, because being shown somewhere again is not a discovery. Queued, so it
  cannot cut off the conversation that caused it.

- **Chain breaks on the dungeon door are announced**, with progress:
  **"A chain breaks on the dungeon door. 2 of 5."**, or **"The last chain breaks. The dungeon
  door is open."** for the fifth.

  `BaseChainDoor.PlayRoutine` marks it with a twelve-second camera hold, a sound and an
  animation, and no text whatsoever. Found only because a tester mentioned chains and a session
  log turned out to contain `SPeaker game object addedCHAIN DOOR`.

  It watches `DataManager.DoorRoomChainProgress` rather than patching the routine: that is a
  coroutine which increments the counter partway through, so a prefix fires too early and a
  postfix fires when the iterator is created rather than when the chain breaks. Polling a static
  int catches the real moment, costs a field read per frame, and cannot be broken by a rename.
  Only forward steps announce — loading a save moves the counter from -1 to whatever was already
  reached, and announcing four chains at the title screen would be worse than silence.

  Note it fires on walking within eight units of the door, **not** on the boss dying. In the
  session that found it, that was some 2,800 log lines after the kill, which is precisely why
  nothing connected the two.

- **`[silent sequence]`, a probe for progress that carries no text.** The two above are the same
  shape: `GameManager.OnConversationNext` holds the camera on an object, no dialogue follows,
  and a text-driven reader has nothing to find. There are around **a hundred** such call sites
  and most of them do have dialogue, so which ones are silent is a measurement rather than an
  audit.

  Every hold is recorded; any dialogue reaching the reader clears the pending ones, since a hold
  framing something spoken is not what this hunts. Anything surviving 1.5 seconds without text
  is reported with the held object's **component types** as well as its name, because the name
  is frequently just "GameObject" and the component is what identifies the moment. 1.5 s sits
  comfortably under the shortest hold in the game, three seconds, so a real one-liner is never
  mistaken for silence. Behind `Diagnostics/LogSilentSequences`.

### Confirmed in play

Several things that had gone unheard for weeks landed in one session:

- **The flying-worm melee adapter: 5 hits, 5 warnings**, `warningSourceMatch=True` on every one.
  That family previously had six unwarned hits.
- **The held room-clear works.** Four `OnRoomCleared` events inside one leader encounter
  produced exactly **one** "Room clear.", via `confirmed-quiet` — three false calls suppressed.
- **`EndOfRunDoor` worked first time**, written entirely from decompiled source and never
  runtime-tested: `continue available=False`, spoken as "sealed; this is the last room of the
  run".
- **Rate-encoded enemy distance is working.** `AmbientEnemy` counts per second now reach 14
  against a ceiling of about 5.7 under the old fixed interval. The ambient budget has still
  never been hit.
- **Spawner detection** caught `EnemyWormTurret` and `EnemyWormBoss` from the `spawnedEnemies`
  field alone.

### Corrected

- **Controller players do get curse auto-aim.** An earlier entry concluded from `CastSpell`
  alone that acquisition was keyboard-gated. The probe disproved it: 8 of 23 casts acquired a
  target, every one on `lastController=Joystick`, pulling the aim 5 to 58 degrees onto the
  enemy.

  `CastSpell`'s gate is one of three acquisition sites and the least important. The
  per-projectile spell routine has the **opposite** polarity — it re-acquires specifically on
  Joystick — a third site acquires unconditionally, and `GetAutoAimTarget` assigns `AimTarget`
  as a side effect so any call from anywhere sets it. Reading one method and generalising is
  what produced the wrong answer.

  No gap to close: the feature was already working. The probe stays, with the caveat that its
  `offBy` measures against the *nearest* hostile rather than the acquired one, so it overstates
  error when the game locked onto something else.

### Still unheard

Both remaining melee adapters (`EnemyMaggotMiniBoss` never appeared; `EnemyWormBoss` and
`EnemySwordsman` are unadapted and accounted for 7 unwarned hits), the health rate limiter
(**zero deferrals across three sessions** — worth reading the code rather than playing again),
and every fix in this entry.

### Changed

- **The always-on enemy cue carries distance by repeat rate, per enemy.** Previously every
  ambient category pinged its whole set together on one fixed interval and encoded distance only
  as loudness. For scenery that is right; for an enemy it is the wrong channel. The player's
  report was exact: the cue tells you where something is but not that you have just gone from
  four metres to two, and it routinely gets drowned out.

  Those are one defect, and level cannot fix either half — `AmbientEnemyVolume` was already at
  its maximum of 1.0 when it was reported, against event cues reaching 1.0 and a measured
  `AmbientEnemy` peak of 0.19 to 0.66. Rate can: a changing rhythm survives masking far better
  than an absolute level, and it is a channel the player already reads fluently, because the
  navigation beacon has encoded distance that way from the start (`0.85s` out to `0.14s`).

  Each hostile now keeps its own schedule instead of sharing the category's, so enemies at
  different distances desynchronise — being surrounded sounds like several rhythms at once, with
  the fastest one naming the nearest. Per-source timing is affordable only here, because
  `Health.team2` is a registry the game maintains, so a hostile can be keyed by instance id
  across frames; every other category still shares one timer.

  The configured `AmbientEnemyRepeat` becomes the rate at the edge of the radius, so a player who
  had already tuned it keeps what they chose and gains a floor beneath it. Capped at 0.18s
  regardless, because several hostiles at arm's length would otherwise merge into a buzz that
  carries no distance at all. Still drawing on the same per-second ambient budget as everything
  else, nearest first, so a crowded room degrades by dropping the far ones.

  **The fairness line, since this is combat information.** Continuous distance to an enemy is
  something vision hands a sighted player for free and constantly; replacing it is equal access.
  "This swing will connect" is a judgement vision does *not* supply — sighted players estimate it
  and miss — so a binary in-range indicator would be more than parity, and is not being added.
  Confirmed against the game: there is no range reticule, no enemy outline (`AddOutline` binds to
  `Interaction`, not to enemies), and the swipe prefab appears as the attack rather than before
  it. Everyone eyeballs the range.

### Added

- **`[curse aim]`, one line per curse cast**, recording which input device the game believed was
  last used, whether it acquired an auto-aim target, where the shot actually went, and how far
  that was from the nearest hostile. Behind `Diagnostics/LogCurseAim`, default on, log-only.

  It exists to settle a specific question. `PlayerSpells.CastSpell` acquires an auto-aim target
  only inside `if (GetLastActiveController(playerFarming).type == ControllerType.Keyboard)`, and
  the charge-up reticule path is gated identically. Read from source that means a controller
  player never receives an acquisition a keyboard player does — and it is generous when it runs:
  a 180-degree arc, scored on angular deviation and distance, divided by a per-enemy
  `autoAimAttractionFactor`, with the target persisting across shots until it dies.

  **The obvious play test does not work**, which is why this is instrumented rather than tried.
  Pressing a keyboard key sets the last active controller to Keyboard, but firing the curse with
  the pad sets it straight back before `CastSpell` reads it — the test would measure a race, not
  the feature. The probe reads what `CastSpell` actually resolved instead.

  The nearest hostile is computed independently of what the game chose, so a cast that acquired
  nothing can still be scored against what it could have hit. `offBy` is what the gate costs, in
  degrees, rather than in argument.

  If it is confirmed, this is not assist to be weighed under principle 13. It is the game's own
  feature being withheld by input device, which is a different question.

### Documented, not changed

- **There is no aim stick.** Aiming, facing and movement are all the left stick — Rewired
  actions 1 and 0 — and the right stick's secondary axes (96, 97) are read only by `Book` and
  `CameraSubtleMovementOnInput`. Melee always fires along `state.facingAngle` with no assist of
  any kind; the chain weapon's `chainAutoAimAngle` is dead code, its own result discarded.
  Aiming a curse therefore cannot be separated from moving, which makes it a two-step action for
  a player who must first find out where the target is.

## [0.2.0] — 2026-08-24

### Fixed

- **The target list went empty for whole rooms until rescanned by hand.** Reported in play and
  confirmed in the log: `[scan category] name="everything" scanned=4 shown=0`, the Everything
  filter dropping every target it had. The scan behind the catalogue is built once and reused,
  and nothing marked it out of date when the room changed — so after a fight the list held four
  corpses, and on entering a new room it still described the old one. Backslash was the only way
  out.

  A scan costs about **57 ms** on this machine (`scope=scan worst=59.06ms average=56.849ms`),
  which rules out the obvious fix of re-scanning whenever a filter is applied. Instead the
  catalogue is now *marked* stale — when the A\* graph is replaced, which is the game changing
  rooms, and when a room is cleared — and re-scanned 0.75 s later, once. The delay matters:
  the graph is replaced before the new room's contents finish spawning, so scanning on the bare
  signal would have produced a confidently empty room, which is the same bug wearing a hat.

  `Navigator.Tick` now runs its graph hook and this check **above** the "am I guiding?" early
  return. Keeping room-change detection behind that return is why a new room stayed stale for
  anyone who was not already tracking something.

  Partly a regression of my own making — the automatic switch to the enemies filter at combat
  start applied a filter to that stale scan at the exact moment it was most stale — and partly
  pre-existing: manual filter cycling hit it too, three times in the same session.

- **A blue heart picked up during a fight was announced as nothing at all.** The combat rate
  limit folds health changes together for up to 2.5 seconds and reports the net, so a gain
  arriving while a drop was waiting cancelled against it and the pair produced no sentence.
  Deferring is now abandoned the moment anything that is not a further drop arrives: gains and
  capacity changes are rare, always worth hearing, and are precisely what a netting fold erases.

- **The health rate limit left no trace, so a session log could not say whether it had ever
  fired.** 17 announcements for 17 damage events was equally consistent with "correctly did
  nothing" and "never ran". Deferrals are now logged with the resulting health and the gap since
  the last one. My own instrumentation gap, and the same defect this project keeps fixing
  elsewhere.

- **Untranslated keys were being read out in capitals.** The 2026-08-23 session log had the
  player hearing "COLLECTED RESOURCES CHEST", "MEAL BAD MEAT" and "PLACEMENT REGION" — shouted
  text where a name should be, which a screen reader treats differently from ordinary words on
  top of simply reading badly. `InteractionScanner.LocalisedName` falls back to humanising the
  term's last path segment when no translation is found, which is right, but `RichText.Humanise`
  only splits separators and camel case and never touched casing.

  Fixed with a separate `HumaniseKey`, used only on that fallback path. Deliberately *not*
  folded into `Humanise`, and the offline harness is why: doing it there title-cased a follower
  called PETERI into "Peteri". The player named them that, in caps, on purpose. Casing can only
  be corrected where the caller knows it is looking at a key rather than a name.

- **A missing translation could pass as a translation.** `RichText.IsUsableLocalization`
  compared the reply against the whole term including its path, so a lookup for
  `Structures/COLLECTED_RESOURCES_CHEST` that echoed back the bare key was not equal to the term
  and was accepted. It now also rejects a reply equal to the term's last path segment — but only
  when that reply still looks like an identifier, because `Items/Meat` really does translate to
  "Meat" and rejecting that would lose a correct name.

- **Descriptions used as labels brought their punctuation with them.** The resources chest is
  labelled "Followers deposit resources here while you are away.", and the sentences it is
  composed into add their own full stop, so guidance said "Guiding to the chest..". One game
  label ends in a bare pipe, which is layout debris. Both are trimmed now; anything ending in a
  letter, digit or bracket is untouched.

- **A label repeated in the name is dropped.** "MEAL BAD MEAT, Meal" said it twice. The existing
  check only looked one way — whether the action contained the structure name — and was a plain
  substring test. It now checks both directions and compares whole words, because "Cook" is a
  substring of "Cooking Fire" and dropping it there would have left a building with no verb.

- **Cue positions were inflated by height.** `CuePlayer.TrySpatialize` projected a source's raw
  `transform.position` through `Camera.WorldToScreenPoint`, and Z in this game is height rather
  than ground position. Any airborne source therefore reported a pan and pitch for somewhere it
  was not. `EnemyMaggotMiniBoss.DiveMoveRoutine` arcs its whole transform through
  `midpoint + Vector3.back * 5`, putting it roughly 2.5 units off the floor at the apex, so its
  cue swung up-screen and across while the shadow, the game's own jump and land sounds and the
  damage collider all stayed on the ground — three positions for one enemy. The target is now
  flattened onto the listener's depth first, which is also what the help text has always
  promised: pitch means "north and south across the ground, not height".

  This is the same defect `RouteFollower.CurrentWaypoint` already fixed for navigation
  waypoints, where camera projection otherwise turned a northward step into a southward
  bearing. The fix had gone into navigation and never into the cue layer.

- **Ambient range was measured through the air.** `SoundscapeSources.Consider` and
  `Soundscape.PlayCategory` both used 3D distance, so an airborne source was charged for its
  altitude: against a ten-unit enemy radius, the diving miniboss spent a quarter of the range on
  height and could fall below the audible floor mid-dive, directly overhead and about to land on
  the player. Both are planar now, matching each other, the spoken distances and the beacon.

- **"Room clear." was announced two or three times inside one miniboss fight.** A session log
  caught `Combat, 2 enemies.` → `Room clear.` → `Combat, 4 enemies.` → `Room clear.` →
  `Combat, 1 enemy.` → `Room clear.` The mod was faithfully reporting
  `RoomLockController.OnRoomCleared`, but `DungeonLeaderMechanics` raises that at every stage
  boundary of a leader encounter, not once per room. Sighted, it is an invisible signal driving
  music, lighting and the chest reveal; spoken, it is the most dangerous sentence the mod can
  produce, because it means "stop fighting and find the exit".

  `doorsDown` looked like the discriminator and is not — both miniboss call sites pass true.
  `DungeonLeaderMechanics.Instance` is, being set once when the encounter begins and nulled once
  when it ends. Inside one, the clear is now held and believed only after the room has stayed
  empty for three seconds; a wave arriving first cancels it and announces the new count instead.
  Ordinary rooms are unchanged and still announce immediately.

- **Health narration drowned out the fight.** Nine `Health dropped to…` lines went out at
  interrupting priority in about forty seconds in the room where a run ended, plus three fervour
  lines — continuous speech over the only cues that could have prevented it, all of it reporting
  a number available on demand at any time. The existing settle window coalesces one damage
  event, not a sustained fight. Drops are now spaced at least 2.5 seconds apart while combat is
  active, by deferring the pending entry rather than dropping it, so nothing is lost and the
  sentence that goes out states the current total. One heart or less always speaks immediately.

### Added

- **Controller hotkeys, as a layer held open with the left trigger.** An entire session went by
  without the enemy beacon being engaged once, and the reason was not that anyone disliked it:
  play is on a pad, and putting it down to reach a keyboard costs more than the beacon is worth
  mid-fight. A combat feature reachable only from the keyboard is not reachable.

  `BindingDump.LogControllerBindings` was added first and run against the game, closing the
  outstanding half of the input gate. It reports, per connected pad, every element the hardware
  exposes, every binding grouped by map category, and the free set both per category and
  overall. The result decided the design rather than the other way round: on an XInput pad,
  `Left Trigger` is the *only* genuinely free element, and the three reported free in every
  category are two Rewired compound-stick aliases that are not separately pressable and the
  Guide button, which Steam and Windows both intercept. Everything else carries three or four
  actions already — `A` is Interact, AdvanceDialogue, PlaceMoveUpgrade, UI Confirm and
  PlaceSticker; `Y` carries seven. Hunting for a spare button was never going to work.

  The game's own reading of the other elements is suppressed while the trigger is held, or
  every press would fire twice — once as a mod command and once as Interact or Dodge.
  `InputSource.GetButtonDown`, `GetButtonHeld`, `GetButtonUp` and `GetAxis` are the four funnels
  every category source inherits, so suppressing there covers gameplay, menus and photo mode
  without enumerating actions. `GetButtonUp` is included deliberately: several game inputs are
  release-triggered, and `GetDodgeRollButtonDown` is literally `GetButtonUp`.

  Movement survives. The D-pad feeds the movement axes as well as the button funnel, so the axis
  patch hands back the analogue stick's own value — removing the D-pad's contribution exactly
  rather than zeroing movement — and does it for the UI axis pair as well as the gameplay one,
  or stepping a filter inside a menu would also move the menu's selection.

  Defaults: D-pad up/down steps the target filter, left/right steps targets within it, `A`
  guidance, `B` enemy roster, `X` beacon, `Y` where am I, shoulders re-scan and repeat
  direction, stick clicks autowalk and silence, `Back` help, `Start` settings. Every command is
  a separate config entry under `[ControllerLayer]`, so a bad value names the command it broke
  and BepInEx writes the valid elements beside it. No in-game rebinding page yet.

- **The forward door of a miniboss room is now described for what it actually is.** That room is
  the last of the run — the map's final node is typed `MiniBossFloor` — and
  `EndOfDungeonContinue` activates a chest there instead of the teleporter unless the dungeon has
  already been completed. The door objects remain in the world regardless, so it is physically
  present and entirely inert.

  Nothing on the door said so. It is not `ConnectionTypes.False`, it has no `RoomLockController`
  holding it shut, and the state that decides lives on a sibling teleporter object — exactly the
  shape principle 3 warns about, and we would have routed the player to a door that does nothing
  and then announced arrival. It now reads as "sealed; this is the last room of the run" and is
  excluded from available exits. When the teleporter *is* active it says "opens a new adventure
  map rather than continuing this one", because that is what taking it does: `Door`'s `NextLayer`
  branch rolls a fresh map and increments `DungeonEndlessLevel` rather than advancing this one.

  Written from the decompiled source and **not yet confirmed in play** — no miniboss room has
  appeared in any analysed session. The lookup is one cached `FindObjectOfType` per room,
  invalidated on the same graph-replaced signal the target catalogue uses, and the miss is cached
  too, since almost every room has no such controller.

- **A non-stereo audio output is now announced at startup, not just logged.** Every direction
  cue this mod makes is stereo pan — left and right is position, which is what the help text
  promises — so a surround mix blurs our positioning, and the game's own spatialised sounds can
  be routed to speakers that do not physically exist while music and menus keep playing. That
  combination was reported in play as "the game's sounds are broken", and it took a device-name
  comparison across session logs to spot.

  Windows is the reason this needs saying out loud. On the machine that hit it, the endpoint was
  still reporting 8 channels *after* spatial sound had been turned back off — so the state was
  wrong without anyone having changed anything. A sighted player would eventually go and look at
  the sound settings; a player whose only channel is audio has no way to notice at all.

  This is the deliberate exception to diagnostics being log-only: a line in a log cannot reach
  someone whose game has just gone quiet. FMOD's enum names are translated first, because
  "_7POINT1" is not a phrase. `Speech/WarnOnNonStereoOutput` turns it off for anyone running
  surround on purpose.

- **Enemies that generate other enemies are named as spawners and listed first.** A session log
  caught a fight whose enemy count ran 2, 3, 4, 3, 2, 3, 2, 3, 2 — the player killing spawn as
  fast as it arrived. The source was an `EnemyWormTurret`, which keeps a list of what it has
  spawned and, on its own death, deals every one of them their full health in damage; its brood
  also carries `GiveXP = false`. So killing the turret ends the fight instantly and killing
  anything else is worth nothing. A sighted player sees the big worm; we announced "Combat, 2
  enemies" and said nothing that told the source from its output.

  Detected from the game's own structure rather than a list of type names: every spawner in this
  game declares a private `spawnedEnemies` field. The element type varies between `Health`,
  `UnitObject` and `GameObject`, so the field *name* is matched across the type hierarchy, with
  names only as a fallback. Cached per component type, negatives included, since almost nothing
  is a spawner and the miss is the common case.

  They sort to the top of both the enemy roster and the enemies filter. Burying the one target
  worth reaching partway down a list that is stepped one press at a time is close to not
  offering it at all.

- **The enemies filter no longer announces itself and then hands over an empty list.** Combat can
  start faster than a new room's scan settles, and the automatic switch to that filter was
  applying the previous room's scan: `enemies scanned=5 shown=0` immediately before "Combat, 2
  enemies. Targets: enemies." The contradiction is now its own trigger — an empty enemies filter
  while combat reports live enemies forces a scan, with no settle delay because the enemies
  demonstrably already exist.

  Rate limited to once every five seconds, and it has to be: `Refresh` applies the category again
  on its way out, so a filter still empty afterwards — everything out of scan range — would have
  re-armed the scan it had just finished and spent 57 ms doing it, forever.

- **Empty target filters are skipped when cycling.** A session log had 85 filter presses produce
  37 "Nothing nearby in ..." replies — followers, story and quests, facilities, each in turn. In
  a dungeon almost every filter is empty, and making cycling cheap on the controller is what
  turned that from untidy into intolerable. Skipping hides nothing, since a filter is passed over
  only when it holds no targets; Everything is the backstop when the whole world is empty, so the
  cycle always lands somewhere and the player still hears why.

- **The controller D-pad now steps targets on up/down and filters on left/right**, the opposite
  of the first arrangement. Up and down being the items matches how a screen-reader user already
  moves through a document, and it is the way round that felt right in play.

- **The target filter follows combat.** When a fight starts the list switches to the existing
  `enemies` filter and switches back when the room is clear, folded into the sentence that was
  going out anyway: "Combat, 3 enemies. Targets: enemies."

  This is what lets the D-pad mean one thing all the time. The two alternatives both cost more:
  a separate combat mode spends one of about fifteen buttons and adds a mode to track by ear at
  the moment that is hardest, and silently remapping the D-pad during combat gives one press two
  meanings. Switching the category instead leaves every control meaning exactly what it always
  means — `enemies` is already one of the nine filters and stepping it already works. Nothing is
  locked; any other filter is still reachable mid-fight, and a filter the player chooses during
  the fight is not overridden on the way out.

- **Landing on an enemy in the target list points the beacon at it**, via
  `EnemyRadar.LockBeaconOn`, which keeps the radar's own index in step so its tick still drops
  the lock when that enemy dies. Stepping the list and locking the beacon were two gestures on
  two keys, which is affordable at a keyboard and is not affordable mid-fight on a pad.

- **`EnemyRadar.CycleBeaconTarget` takes a direction.** A forward-only cycle through five
  enemies to reach the one behind you is the sort of thing that stops a combat feature being
  used at all. Either direction still passes through "off" once per lap, so the beacon can be
  dismissed without counting what is left.

- **Melee wind-up cues for the Forest Flying Worm and the Forest Miniboss Diving Maggot**, the
  two families that accounted for eight unwarned hits in the last session — the miniboss's
  carried `warningAge=87.0`, meaning no warning had fired for a minute and a half.

  `EnemyBat.AttackRoutine` is the same shape as the already-adapted scuttle-swiper, with a
  literal `Duration = 1f` wind-up, the longest telegraph adapted so far. No instance-type filter
  is needed there: Harmony patches the declared body, so an override that replaces it never
  reaches the cue and one that calls `base` runs this exact timing.

  The maggot needed a different hook. Its dives all live inside one `DiveMoveRoutine`, so a
  prefix would fire once for a run of three or four leaps; `GetNewTargetPosition` is public,
  called from exactly one place at the top of each iteration, and its return value is the game's
  own "this dive is happening". Lead time is the game's own `distance / MoveSpeed`, read live so
  the 1.35x speed boost it gives itself in its second phase is picked up for free.

- **A melee warning can now be aimed at where an attack will land** rather than at the attacker,
  via an optional impact position through `MeleeWarningSchedule`. A standing swing hits next to
  whoever threw it, so the attacker is the right thing to point at; a leap is not like that. The
  diving maggot commits from one side of the room and its damage collider is enabled for 0.3 s
  where it lands, so pointing at the maggot sent the player toward the impact.

- **`[cue audio] census`**, a once-a-second tally of which cues actually sounded, with a count
  and peak volume for each, behind `Diagnostics/LogCueAudio` (default on). Cues previously logged
  once, the first time each ever played, which cost a diagnosis directly: asked whether the enemy
  proximity cue was audible in the room where a run ended, the log could not say, because
  `AmbientEnemy` had logged near startup and nothing since. Per-play logging is not an option —
  wall tones alone run four directions every 0.15 s — but a per-second tally makes masking
  arguments arithmetic instead of recollection. The companion to `LogSpeech` for the non-speech
  half of the output.

- **Autowalk.** **Delete** walks the Lamb along the route walking guidance is announcing, and
  stops again if pressed a second time. If guidance is not already running it starts it to the
  selected target first, so from a chosen target autowalk is the only key needed.

  It is a fallback and a probe rather than a way to play, and it exists for two jobs: getting
  past a stretch where following spoken instructions has become tedious or is not working, and
  answering whether the navigation code is at fault when a player reports being lost. A route
  autowalk cannot drive either is a route problem; one it drives cleanly points at the
  instructions instead. Giving up emits `[autowalk] no progress` followed by
  `[nav reachability] event=autowalk-stuck`, so that failure arrives carrying its own evidence
  — the player and target graph areas, and whether the target node is walkable at all.

  This is still automation, which the project's own principle 13 rules out by default, and the
  governing ruleset permits only with explicit permission and as a toggleable choice. Both
  conditions are met and the shape is deliberately narrow. Autowalk decides nothing: it
  executes the instruction guidance has already spoken and stops the moment guidance does.
  There is no second path, no opinion about where to go, and nothing reachable by it that a
  held direction key could not reach. It never engages on its own, and the key can be disabled
  entirely under Wayfinding in the settings menu.

  Implemented by supplying the game's own two gameplay movement axes through a Harmony postfix
  on `RewiredGameplayInputSource.GetHorizontalAxis` and `GetVerticalAxis`, rather than by
  moving the character. Speed, acceleration, collision, facing, the analogue speed curve and
  the game's own invert-movement setting are all downstream of those two numbers, so none of
  them is reimplemented and none can drift from what the game does for a player holding a
  direction. The heading is the live A* waypoint at full precision rather than one of the eight
  compass points the instructions are spoken in, which is why it arrives closer to the target
  than following the words does.

  Where it deliberately lets go:

  - **The player's own input, while held.** Anything past a 0.15 deadzone hands control back,
    and autowalk resumes on release. Not an off switch, because stepping round an obstacle is
    the common case and having to re-engage afterwards would make the feature not worth using.
    The deadzone sits below the game's own `MinInputForMovement` of 0.3 on purpose: between the
    two figures the player is pushing and the game has not yet moved them, and driving through
    that band would steer against them.
  - **Any state that is not walking.** An allowlist of the six states in which
    `PlayerController` turns those axes into movement. The same two axes are also the aim
    direction, the building placement cursor, the map pan, the fishing reel and the dodge-roll
    steer, so attacking, dodging, building and every scripted sequence stay entirely the
    player's.
  - **Anything that stops the mod's update loop** — the settings menu, the follower wheel,
    speech switched off. The driving flag is frame-stamped rather than latched, so a tick that
    stops being called releases the direction on its own instead of leaving it held.
  - **Three seconds without covering ground**, announced. A sighted player sees the Lamb pinned
    against scenery; autowalk removes the one signal that would otherwise give it away, which
    is the feeling of holding a direction and getting nothing back.

  One side effect stated rather than left to be discovered: these are the gameplay axes, not
  walking-only axes, so `EnemyExploder`'s lock-on bias reads the heading too while autowalk is
  on — the same thing it would do for a player holding that direction.

  The two judgements that are not about the game — when the player has taken over, and when
  driving has stopped getting anywhere — are in `AutowalkPolicy`, pure and covered by the
  offline harness, because they decide whether the mod is holding a movement key the player
  cannot see it holding.

---

## [0.1.1] — 2026-08-22

### Fixed

- The installer failed to start when unpacked to a path containing spaces, such as
  `C:\games\cult of the lamb\`. `Start-Process` joins its argument list with spaces into one
  command line, so an unquoted script path arrived at the elevated PowerShell as
  `-File C:\games\cult` and nothing ran. Only the self-elevation branch was affected; the
  already-elevated path was correctly quoted.

- The installer narrowed TLS instead of widening it. Both network calls assigned
  `SecurityProtocol = Tls12`, and assignment replaces the set rather than adding to it — so on
  a current Windows, where the default is `SystemDefault` and the OS may negotiate TLS 1.3, the
  installer actively downgraded a healthy machine. A downgrade is exactly what an
  HTTPS-inspecting antivirus or proxy turns into a handshake failure. `SystemDefault` is now
  left alone, and anything else has the modern protocols added rather than substituted.
- A failed Thunderstore lookup returned nothing and said nothing, so a TLS or proxy problem
  was indistinguishable from "no link available". The reason is now reported into the progress
  box; the fallback to the package page is unchanged.

### Added

- Guidance in the installer for certificate and SSL errors on the download link, which are
  the security software inspecting encrypted traffic rather than a problem with the file.

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
