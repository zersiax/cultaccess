# CultAccess

A screen-reader accessibility mod for **Cult of the Lamb** (PC / Steam), built on BepInEx 5.

Goal: make the game playable without sight — menus, dialogue, inventory, rituals, and
crucially the base-building layer.

## Status

In active development and verified in-game through the opening dungeon and the first cult
building tutorial. See the roadmap below for the remaining gaps.

## Requirements

- Cult of the Lamb, Steam, Windows x64
- A screen reader. NVDA works out of the box (the mod ships NV Access's controller
  client). JAWS / ZoomText / SuperNova work if you drop a 64-bit `Tolk.dll` and its
  adapters into `BepInEx/plugins/CultAccess/`.
- Windows SAPI is used as a last-resort fallback so the mod never goes completely silent.

## Install (from source)

The build references the installed game directly, so there is nothing to restore and
no NuGet feed involved.

```sh
dotnet build src/CultAccess/CultAccess.csproj -c Release
```

That deploys `CultAccess.dll` plus the native speech libraries into
`<game>/BepInEx/plugins/CultAccess/`. If the game is not on the default path, override it:

```sh
dotnet build src/CultAccess/CultAccess.csproj -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Cult of the Lamb"
```

For a compile-only check that does not write to the Steam install, use
`-p:DeployOnBuild=false`. The offline pure regression harness runs with:

```sh
dotnet run --project tests/CultAccess.PureTests/CultAccess.PureTests.csproj -c Release
```

BepInEx itself is already installed into the game folder. To remove everything, delete
`BepInEx/`, `doorstop_libs/`, `winhttp.dll`, `doorstop_config.ini` and `run_bepinex.sh`
from the game directory.

## Hotkeys

- **F1** — speak the mod's key assignments, one section per press
- **F2** — open or close the mod's own settings menu, including Learn sounds
- **F7** — stamp a numbered marker into the log at this instant
- **F8** — read the open panel's body text (tutorial explanations, popup prose)
- **F9** — speak which speech backend is active (setup diagnostics)
- **F10** — stop speech
- **F11** — repeat last announcement
- **F12** — re-read the current build cell, radial-wheel choice, focused menu item,
  or live health, fervour, active tarot-card count, and cult tutorial step when no menu is open

Walking and navigation. Every command has two bindings, and the first set is the one to
prefer — it follows the conventions screen-reader users already know from moving through
documents and lists, and it sits in the same physical place on every keyboard layout:

- **Page Up** / **Page Down** — cycle to the previous / next nearby target
- **Ctrl+Page Down** / **Ctrl+Shift+Page Down** — cycle the target filter forward / backward:
  everything, travel and exits, actions now, facilities, resources, story and quests,
  enemies, followers, or characters
- **Home** — start or stop walking guidance to the selected target
- **End** — speak the current walking direction now
- **`\`** — re-scan surroundings

The same commands are also on a punctuation cluster, which may suit you better if your hands
are already there. This is particularly useful in combat:

- **`[` (left bracket)** / **`]` (right bracket)** — previous / next nearby target
- **`/` (slash)** / **Shift+`/` (shift+slash)** — cycle the target filter forward / backward
- **`; (semicolon)`** — start or stop walking guidance
- **`' (quote)`** — speak the current walking direction

**A caveat on the punctuation keys.** They are bound by the character they type, not by where
they sit. On a QWERTZ or AZERTY keyboard the key that produces a semicolon is somewhere else
entirely, so the cluster stops being a cluster. That is why the Page Up and Page Down set
exists and why it is listed first: those keys are in the same place on every layout. Both sets
are rebindable in config.

Combat:

- **`. (period)`** - list nearby enemies with distance and direction
- **`, (comma)`** - point the audio beacon at the next enemy; cycle beyond the last to turn it off

Inside the settings menu only:

- **Up** / **Down** — move between rows
- **Left** / **Right** — change the focused value
- **Enter** — open a section, or run an action such as playing a sound
- **Backspace** — go back one level, and close the menu from the top
- **Home** / **End** — jump to the first or last row

All rebindable in config, apart from the menu's own navigation keys.

**Confirmed safe.** The game's bindings live in Rewired asset data, not in code, so they
cannot be read from a decompile — `Diagnostics / LogGameBindings` dumps them from the
running game. It reports 22 keys in use:

`A B C D E ESC F J K L LeftShift N Q R S Space T Tab V W Y Z`

Every mod hotkey above avoids all of them. Worth noting what that ruled out: `E`
(Interact / AdvanceDialogue), `Q` (hold to Return to Base; historically named Ability in
the input map), `R` (Interact 2) and `T` (Interact 3 and active Fleece Ability) are all taken,
so the obvious letter choices for a navigation cluster would each have stolen a gameplay key.
Re-run the dump after any game update, since bindings can change.
(note to hoomans: you can probably safely stop reading here)

## Directions

North is **screen up**, and bearings are computed by projecting through the camera rather
than from world axes. Because the camera is fixed and top-down, "north east" means push
the stick up and right — no mental rotation. Projecting through the camera also keeps this
correct if the camera ever rotates, and avoids depending on whether the game's ground
plane is XY or XZ.

Targets come from six sources, deduped so one character never appears twice:

- **Exits** — the game's live `Door.Doors` registry. Dungeon room doors trigger by walking
  through them; they are not E interactions. The mod says `open` or `locked` from the
  attached `RoomLockController.Open` state, and points out the nearest forward exit on a
  rescan. The cult's goop gateways are also walk-through passages rather than button-driven
  interactions. While one is closed its live objective is reported as the blocker; after the
  wall lowers it is called open, guidance aims through the wall instead of at its centre, and
  crossing into the Old Faith approach refreshes the travel list automatically. The four
  dungeon entrances there start as follower-gated E interactions: `1 / 7` means one current
  follower out of seven required, not a one-follower requirement. The scanner states those
  counts in that order and will not guide to a threshold that has not been met. Once an
  entrance opens, it becomes a walk-through passage; the mod follows the game's separate
  transition collider, keeps the entrance available, and says not to press E again. Because
  that collider extends beyond DoorRoom's walkable graph, A* first guides to the reachable
  door threshold; an immediate final direct instruction then continues through the live
  transition trigger without requiring the player to stop.
- **Interactables** — anything with a use prompt, named with the game's own localised
  label and grouped by its concrete game type or owning structure: facilities, natural
  resources, story and quest targets, and travel. `Actions now` is the cross-category view
  of interactions the game's live selection and action gates confirm as usable. Both that
  view and Everything retain only the nearest usable lumber, stone, and food node so natural
  resources cannot bury other targets; the Resources category remains exhaustive. A disabled
  story conversation can still be listed as a landmark, but is explicitly called `currently
  unavailable`; only doors are ever called locked. If only Interact 2, 3, or 4 is enabled,
  the target says so instead of implying the primary interaction will work. Structure-owned
  actions include their building, for example `Build Cooking Fire` while it is a build site
  and `Cooking Fire, Cook` after completion. Placement and actual finished-prefab creation
  silently refresh the cached target catalogue once the new interaction has initialized.
  Weapon, curse, and relic podiums are named from the concrete item they contain rather than
  their generic in-range `Equip` label. Their target entry, approach prompt, and successful
  equip announcement include the item title, kind, and level; weapon prompts also include the
  same damage and speed values shown by the game. Red, blue, black, fire, and ice heart pickups
  are likewise named from their concrete inventory type: the game's icon-only interaction label
  is literally a period, so it is never exposed as the target name.
- **Followers** — named from `Brain.Info.Name`.
- **Enemies** — current entries in the game's hostile health-team registries.
- **Other characters** — Spine-rigged story NPCs, cultists in scripted scenes, bosses.
  These carry no label anywhere, so they are named from their object name as a last
  resort. An awkward name for something that gates progress beats it being invisible.
  `Diagnostics / LogScanCandidates` logs each one so bad names can be corrected. The
  Lamb's own Spine hierarchy is explicitly excluded.
- **Pending recruits** use the save-backed recruit queue plus the base's authored
  indoctrination-platform transform. The game does not create the real `Indoctrinate`
  interaction after a load until the Lamb comes within eight metres, so this target bridges
  that otherwise unnavigable approach. It appears in Actions Now and Story and Quests and
  never spawns or recruits anything itself.

Guidance follows a real route from the game's own A* graph — the same graph its NPCs walk
on, including the same default walkable-node constraint used by the game's own path-
possibility check — so it routes around walls and water instead of pointing straight
through them. Movement is planar: waypoint proximity and reroute decisions use X/Y, while
graph waypoints are projected at the Lamb's current render depth before deriving a spoken
screen direction. If the player and target are currently in disconnected graph areas, the
target remains pending and
is retried after graph updates and twice per second for moving enemies. Guidance announces
and starts automatically when the route opens. A known closed room barrier, moving barrier,
destructible tile, or spider nest on the direct line is identified with its distance and
direction; destructibles are described as possible blockers unless graph-area samples show
that the collider straddles the two disconnected areas. A straight-line bearing is only used
after an otherwise valid route exhausts its waypoints, and is always labelled `direct line`.
Spoken guidance is continuous rather than step-and-wait: the first instruction says `Go` with
a heading and segment distance, a changed heading interrupts immediately as `Turn`, and
straight-through graph points remain silent. The last segment names both its direction and
destination, while the current instruction still repeats every few seconds if needed.

## Combat cues

Combat uses short generated FMOD earcons rather than screen-reader speech for information that
must arrive within a fraction of a second. Their stereo position carries screen-left/right and
pitch carries screen-up/down, matching the navigation beacon:

- A soft band of noise marks where a dodge is taking you, deliberately one of the quietest
  sounds in the set: it reports something that has already happened, and nothing depends on
  reacting to it. It sounds only for a dodge without
  movement input, where the Lamb's current facing determines an otherwise-unknown travel
  direction; an unobstructed directed dodge stays silent, because it would only tell you what
  you just chose. The same burst followed by a percussive knock means the projected dodge
  corridor contains solid geometry.
- A mid-low pulse marks a wall or other solid collider in the current movement direction. Its repeat
  rate increases as the obstacle approaches, and a broad percussive knock marks contact. It remains
  silent for nearby walls the player is not moving toward.
- A harsh falling buzz marks a supported melee wind-up. The first adapter covers the early
  Darkwood scuttle swiper at its exact attack-coroutine commit point, before its damage
  collider activates. It is deliberately the most aggressive sound in the set alongside the
  trap: by the time it fires the attacker is already within reach of you.
- A high double buzz marks a hostile projectile whose live trajectory is predicted to intersect
  the player inside the configured warning horizon. Volleys are reduced to the most imminent
  threat instead of producing one sound per projectile.
- A lower double buzz is positioned at a grenade landing area when the player is predicted to
  be inside its damage radius.
- Two rising notes mean a dodge actually saved you. This answers a different question from the
  dodge direction cue and the two are worth keeping straight: **dodge direction** fires as the
  dodge begins and says where you are going, while **dodge avoided a hit** fires afterwards and
  says the dodge was worth making. Dodging puts the Lamb in a state the game treats as
  untouchable — `Health.DealDamage` refuses outright while it lasts — and this sounds once per
  dodge, only when a hit was actually thrown away. Hearing nothing after a dodge means nothing
  was going to hit you anyway.
- A metallic rattle marks a static trap that has triggered under you, such as a spike trap. It
  fires when the trap commits, at the start of its own half-second wind-up and before any
  damage, and it is the loudest cue in the set. There is nothing to face and nowhere to dodge
  to; the useful response is to leave.

Combat entry announces the settled enemy count and says `Room clear` when the game's own
combat-room completion event completes the encounter.

Every cue has its own switch and its own volume, in the settings menu on **F2** under Sounds,
or under `Sound Cues` in `BepInEx/config/dev.cultaccess.cfg`. `Diagnostics / LogCombat`
records each dodge corridor, wall cue, projectile prediction, grenade timing error, trap
telegraph, and actual or evaded damage source, so uncued enemy-specific attacks can be added
from real play sessions.

### Why the cues sound the way they do

Every cue is separated from every other along three axes at once — the material it is built
from, its rhythm, and its register — because pitch alone is not enough under combat audio.
Filtered noise is used by the dodge cues and nothing else. Inharmonic metallic strikes are
used by the static trap and nothing else. The dodge confirmation is two discrete notes rather
than a glide.

Level carries meaning too, and separately from timbre. Every cue that means **you are about to
take damage** — melee wind-up, incoming shot, danger area, static trap — is a harsh buzz or
rattle at the same trim, because they ask the same thing of the player and none of them should
be easier to miss than its siblings. Everything that merely **reports** something already
settled sits well below them, the dodge-direction cue lowest of all. An earlier version had
that one at warning volume, which made a report sound like an alarm.

That structure exists because of a real defect. The first cue set described the dodge as a 650
to 1350 Hz rise and the evade confirmation as a 700 to 1450 Hz rise: two sweeps a few percent
apart, in the same register, with the same envelope and the same harmonics. They were
reported as indistinguishable, and they were. The offline test harness now measures the
correlation of every pair of cues and fails the build if two converge again.

## Always-on sounds

Separately from the cues above, the mod can keep a quiet, continuous picture of what is
around you. **Every category starts switched off**, and each is enabled on its own in the
settings menu under Sounds, Always-on sounds.

- **Wall tones** — a *continuous* ring of tones for solid geometry in eight directions around
  you. This is not the same thing as the wall cue in combat: that one warns about the
  direction you are already moving, while this describes the shape of the space you are
  standing in.

  Each direction has its own note as well as its own place in the stereo field, following the
  same rule the beacon already taught: higher means further up-screen. North is the top note
  and south the bottom one, while east and west share a note and differ only in left and
  right. The notes are a pentatonic set, so any combination of directions forms a chord rather
  than a clash — which matters, because several sound at once and they never stop.

  They sustain rather than repeating, and that is the whole point. With a pulse train a
  doorway is a beat that fails to arrive, and the ear cannot tell that apart from a beat that
  is merely late until several more have passed. With a sustained tone, walking past a gap is
  a sound **stopping**. Follow a wall, and the opening is the moment that side goes quiet.

  For the same reason this category has no cap on how many directions may sound at once,
  where every other one does. A direction culled to reduce clutter is silent, and silent is
  exactly how the player is being told they can walk that way, so a cap would invent doorways.
- **Items** — dropped items and hearts lying near you.
- **Interactables** — objects near you that you could press Interact on.
- **Characters** — followers and other characters near you.
- **Enemies** — living hostiles. Reaches further and repeats faster than the rest.
- **Projectiles in the air** — every hostile projectile in flight nearby. This is the
  bullet-hell case: in a pattern dense enough that one warning per shot would arrive too late,
  a steady stream of ticks lets the shape of the fire be heard as a shape.

Volume falls away steeply with distance rather than linearly, which is what makes the layer an
orientation aid instead of a wash: at half its range a source is already well under a quarter
volume, so things are effectively inaudible until you are close to them. Each category has its
own range, repeat rate, and cap on how many things may sound at once, and there is a ceiling
on the total across all categories — reached only in a crowded room, and when it is, incoming
fire keeps sounding and scenery does not. Wall tones are exempt from both, because they
sustain rather than repeating.

## Replacing the sounds

Every cue is generated in code, so the mod works with no audio files at all. Drop a file named
after a cue into the `sounds` folder beside the plugin and it is used instead — no rebuild, no
setting to change, and anything you do not replace keeps its built-in sound.

This exists because which sounds are legible is genuinely personal. It depends on the screen
reader, the headphones, the hearing, and what the player has already trained themselves to
listen for. Shipping one fixed set and calling it accessible would be the same mistake as
shipping one fixed language, so the sounds work the same way the translations do: a guaranteed
built-in default with an editable layer on top.

`.wav`, `.ogg` and `.mp3` are accepted. Learn sounds says the file name for whichever cue you
are on, so you never have to guess, and `sounds/README.txt` lists them all. A file that is
present but cannot be loaded is named in the log and falls back to the built-in sound rather
than failing silently.

Nothing ships in that folder. There is no third-party audio in this mod and nothing to
license.

## The settings menu

**F2** opens the mod's own settings menu. It is spoken and keyboard-driven and draws nothing
on screen. Up and down move, left and right change a value, Enter opens a section or runs an
action, and Backspace goes back and closes the menu from the top.

It covers speech, every sound individually, wayfinding, and diagnostics. Everything in it is
the same setting as the corresponding line in `BepInEx/config/dev.cultaccess.cfg` — the menu
writes through to the config file, so a change made in game persists, and a change made in the
file shows up in the menu.

**The game is not paused while the menu is open.** Nothing here freezes or slows it, because
that would hand you a pause in a dungeon that a sighted player does not get. The Lamb still
responds to W A S D throughout. Open the menu when you are safe.

### Learn sounds

A section of the settings menu that plays each cue and says what it means. Moving onto a row
speaks the name and then plays the sound, so working through the list is itself the lesson.
Enter on a row plays it again as a sweep: left, centre, right, then low, level and high.

That sweep exists to teach the one thing about this mod's audio that is most often
misunderstood. Left and right is stereo position; **the pitch change is vertical aim, not
distance**. Players reasonably assume pitch means distance, because both change together as
you approach a raised target. A few seconds of hearing it move settles it in a way that a
sentence of explanation did not.

### Wayfinding modes

Guidance can use the beacon, spoken instructions, or both, chosen under Wayfinding. The two
channels suit different moments and neither is a reduced version of the other: the beacon is
continuous and costs no listening effort, which is what you want mid-fight, while spoken
instructions name the destination and the distance, which is what you want in an unfamiliar
base.

The mode governs only the **automatic** channels. Refusals, arrivals, and anything you
explicitly asked for are always spoken, in every mode — a preference about ambient chatter is
not consent to be told nothing.

## Radial wheels

Follower command wheels announce the follower, a short choice list, and the exact keyboard
gesture the game's radial requires: hold a direction and press E without releasing it. Each
highlighted command announces its localized title, description, and unavailable state. F12
repeats the current choice, and F1 gives wheel-specific controls. Scanner, guidance,
enemy-radar, and beacon controls are suspended while this modal is open, so they cannot
operate on the world behind it.

The doctrine-category, weapon, and curse wheels use the same focusless radial machinery and
have equivalent structural reading. Doctrine categories require holding the highlighted
direction while pressing E. Weapon and curse highlights apply through the game's own
close-wheel flow. Their localized title, effect, availability, and position are announced,
and world controls remain suspended while any supported radial owns input.

## The game's settings screens, and Twitch

On PC, Settings has five tabs. CultAccess reads the instantiated tab labels and order rather
than assuming them, announces the current tab and position, and speaks the live Rewired keys
for switching tabs. On the current default bindings, Q is previous tab and R is next tab.
A and D adjust sliders and left-right choices; E toggles switches. Those key names are also
read from Rewired, and every actual value change is announced even though the game keeps
focus on the same control. Trying to move past a slider's limit does not falsely announce a
change.

The Twitch setup flow is:

1. Open Twitch Settings from the pause menu and activate Connect. The game opens browser
   authorization and waits for it to complete.
2. The Twitch channel must be live with Cult of the Lamb selected as its category. The game
   does not regard a saved authorization token alone as an active integration.
3. Activate Integration Configuration to open the Twitch extension dashboard, then activate
   and configure the Cult of the Lamb extension there.
4. Return to the game. CultAccess announces authorization and realtime-socket readiness as
   separate states, so a saved login is never misreported as a working live connection.

Connection start, success, inactive saved authorization, socket readiness/loss, sign-out,
and a prolonged browser wait are announced. Twitch follower raffles and chat follower votes
read each overlay's active/result/error text and announce changed participant or vote totals.
Totem completion and the otherwise visual reward-wheel result are announced. Help/Hinder
outcomes, Totem contributions, Twitch drops, and other localized Twitch notification cards
use the normal notification reader; follower messages use the normal dialogue/bark reader.

## Tutorials, cooking, and crafting

Tutorial overlays are real multipage cards even though the menu itself opens only once.
CultAccess reads the configured page after its transition, announces `page X of Y`, and gives
the live left/right and close keys. Every page change is read automatically; F8 repeats the
current page rather than returning to page one.

Cooking and the Tailor's crafting wheel support both of the game's accessibility modes without
changing them behind the player's back. Entering either queue announces whether Auto Cook or
Auto Craft is on before the timing challenge is committed. With the automatic setting on, the
game's own success-window logic supplies the input and CultAccess announces each result, the
remaining queue, and completion. With it off, a short rising FMOD chirp sounds as the visual
tracker enters the exact live success region; press the announced Interact key on the chirp.
Cooking failures identify undercooked/burned output, and Tailor failures report the game's
rounded-down half-material refund. The automatic settings are in Settings, Accessibility and
remain an informed, player-controlled choice.

## Player status and tarot cards

Health changes use the same red, spirit, blue, black, fire, and ice heart pools rendered by
the HUD. Damage interrupts with the settled current/capacity value; healing and special-heart
gains queue behind more urgent speech and identify the gained heart type. Walking over an
automatic heart pickup correlates its localized item name with that exact resulting health
change, replacing the generic state update rather than speaking both. Fervour changes are
coalesced and report both bar percentage and how many curse casts are ready. The optional
`Speech / AnnouncePlayerStateChanges` setting controls automatic updates. F12 always reads the
live health composition, fervour, and active tarot-card count while no menu is open.

Tarot reveals, two-card choices, collection entries, and multi-card fleece rewards read the
game's localized card name and effect. Choice focus includes position and the live confirm key;
locked collection entries remain hidden. Unlock screens wait until the game's Accept prompt is
actually enabled before telling the player to continue. F8 adds the selected card's lore, and
run-card additions/removals are announced from the game's own trinket events.

## Inventory, quests, and progression

The Inventory page names each icon-only resource, food, and item with its owned quantity,
category, and position. Player details identify the equipped weapon, curse, relic, fleece,
crown abilities, fragment counts, levels, and localized effects. These pages are read-only;
the context announcement gives the live page-switch and close keys rather than presenting
their icons as unexplained buttons.

The quest log reads the localized quest heading and every active, completed, or failed
objective. Active entries report completion, tracking state, expiry percentage when present,
and whether E can toggle tracking. The announcement follows the game's three-tracked-quest
limit and respects objectives whose tracking button is deliberately disabled.

During the first Shrine tutorial, Ratau's gifted follower is a separate pending recruit, not
either existing named follower. Navigate to `Indoctrinate new follower`, use E when the real
`Indoctrinate` prompt appears, complete customization, then use the follower command wheel to
assign Worship. The where-am-I status distinguishes this beat from the earlier build-a-Shrine
beat even though the game's underlying onboarding enum calls both of them `Shrine`.

Ritual entries report their localized name and effect, cooldown, one-time completion state,
special follower or structure requirement, faith/warmth/sin effect, and every cost alongside
the amount currently owned. A visually concealed locked ritual remains simply `Locked ritual`;
its identity is not leaked. Story-forced and free rituals follow the button's final game-owned
availability rather than being rejected by an earlier affordability check.

Doctrine choices announce both visible alternatives, category and level, effect, unlock type,
and position. Choosing is a two-stage action: E selects the card, then CultAccess announces
whether the current Hold Actions setting requires keeping E held for three seconds or pressing
it again. The doctrine-history book reads declared choices while preserving the identities of
locked and opposing unchosen choices. F8 re-reads either the focused choice or the choice
currently awaiting confirmation.

Building and crown upgrade trees name every visible node with tier, state, description, and
the exact missing tier, structure, prerequisite, or Divine Inspiration. Their focusless unlock
overlay announces the selected node only after it is ready, gives the hold-or-press instruction
from Hold Actions, and confirms the resulting unlock.

Useful config settings:

- `Speech / Verbosity` — `Low` (labels only), `Normal` (adds role, state, position),
  `Diagnostic` (adds component and object names — use this when reporting a bad reading).
- `Speech / AnnounceMenuContext` — say the menu name when entering a new screen.
- `Speech / AnnouncePlayerStateChanges` — announce health and fervour changes. On by default.
- `Speech / Braille` — mirror announcements to a refreshable braille display. On by default.
  Braille is a separate channel from speech, not a side effect of it: NVDA needs
  `nvdaController_brailleMessage`, and Tolk needs `Tolk_Braille`. Harmless with no display
  attached. Press F9 to hear whether the active backend exposes braille at all.

## How it hooks the game

The game is Mono (not IL2CPP) and unobfuscated, so `Assembly-CSharp.dll` decompiles
cleanly and we can reference its real types at compile time.

Notes worth keeping, because they were not obvious:

- **Focus changes: patch the private `UINavigatorNew.NavigateTo`.** There is a tempting
  public `UINavigatorNew.OnSelectionChanged` event, but it only fires for directional
  navigation — it misses mouse hover (`NavigateToNew`) and the default selection set when
  a menu opens. `NavigateTo` is the one place `_currentSelectable` actually changes.

- **Compound widgets are proxy + control.** A focused `MMSelectable_Toggle` holds a
  reference to the real `MMToggle` living elsewhere in the hierarchy; same for
  `MMSelectable_Slider`, `MMSelectable_HorizontalSelector`, `MMSelectable_Dropdown`.
  Reading only the focused object gives you a label with no state.

- **Text is full of TMP markup.** The game colours numbers via a `.Colour()` string
  extension and inlines resource icons as `<sprite>` tags. `Util/RichText` strips markup,
  resolves confirmed resource sprites, preserves icon nouns in labels such as `Chop Lumber`,
  and suppresses the icon when nearby prose already says the same thing.

- **Localised strings are free.** I2 Localization is compiled into `Assembly-CSharp`;
  `LocalizationManager.GetTranslation(key)` gives translated text for any key.

- **Input is Rewired**, so the mod reads the keyboard directly rather than going through
  the game's binding system. Our hotkeys cannot collide with player rebinds.
  `Input/Keys` falls back to the new Input System if legacy `UnityEngine.Input` is
  disabled in the build.

- **Native DLL loading**: `Speech/NativeLoader` pins the screen-reader DLLs by absolute
  path with `LoadLibraryW` at startup, so `DllImport` resolves them from the plugin
  folder instead of requiring them in the game root.

## Base building — why this is tractable

The main worry going in was conveying a 3D isometric world non-visually. It turns out
base building is **not** free-form 3D placement. `PlacementRegion` maintains:

```csharp
Dictionary<Vector2Int, TileGridTile> GridTileLookup;   // the whole buildable area

class TileGridTile {
    Vector2Int Position;      // discrete grid coordinate
    Vector3    WorldPosition; // where it is in the scene
    bool       Occupied;
    bool       Obstructed;
}
```

There is a `PlacementTile` per visible cell carrying a `Vector2Int GridPosition` and a
validity colour (red = blocked, green = valid, orange = edit mode), plus 8-direction
neighbour helpers (`GetVector3FromDirection`) and lookups by world position
(`GetTile`, `GetClosestTileAtWorldPosition`).

So the non-visual model is a **2D grid with per-cell occupancy and validity** — the same
shape of problem as a spreadsheet, not a 3D scene.

The first implemented slice uses the game's existing build controls. Catalogue focus announces
the localized structure name and description, exact availability or reason, resource costs
with owned totals, and list position. Once placement begins, each move of the W/A/S/D grid
cursor announces screen-relative direction, raw grid coordinates, cell contents, and the
result of the same `PlacementRegion.IsValidPlacement` check used by E. Rotation and completed
new buildings are announced; F12 repeats the current cell. Edit, move, upgrade, path, ranch,
and fence modes use their specific game-owned eligibility gates. The mod observes this state
and does not move the cursor or confirm anything for the player. A successful placement also
adds its named build interaction to navigation without requiring Backslash; when construction
finishes, the catalogue refreshes only after the asynchronously loaded finished prefab and its
own actions exist.

The objective HUD is mirrored as well: new, changed, completed, and failed objectives announce
their localized group title and live objective text. Updates settle for 0.35 seconds of quiet,
so synchronous event pairs and multi-item harvest counters fold into one final message rather
than producing stale or rapid-fire intermediate announcements.

Next layers:

1. **Structure queries** — "what is here", "what is adjacent", and "nearest free valid cell
   for the selected building".
2. A **virtual grid cursor** for inspecting the base without entering placement mode.
3. **Jump navigation** — cycle between structures of a type, region edges, and the
   player's own position, so the user never has to sweep the whole grid.
4. **Audio beacons** for continuous targets (a follower, the current objective), using
   FMOD with Google Resonance Audio — both are already in the game, so HRTF-spatialised
   cues are available without adding an audio stack.

Free-roaming combat and dungeon movement are a genuinely harder problem than the base
and are deliberately deferred until the base layer is solid.

## Audio beacon

The beacon plays through **FMOD**, not Unity audio, and that is not a stylistic choice.
An AudioSource implementation was completely silent here: the diagnostic reported
`clipSamples=0` and `isPlaying=False`, the signature of a project built with Unity audio
disabled — which an FMOD-only title naturally does. Unity hands out AudioClips and
AudioSources that never make a sound. FMOD is provably running, since the game plays
through it.

Encoding, because stereo alone cannot express a 2D bearing:

- **Pan** carries left/right
- **Pitch** carries up/down, higher being further up-screen
- **Ping rate** carries distance, speeding up as you approach

Beacon volume is independent of the game's own sliders, hence its own setting. The **Learn
sounds** section of the settings menu can play it moving left to right and low to high, which
is the quickest way to internalise that the pitch change is vertical aim rather than distance.
Under Wayfinding you can also choose whether guidance uses the beacon, spoken instructions, or
both.
The comma-key enemy lock temporarily takes priority over a walking-guidance beacon and points
directly at the enemy transform. Turning enemy tracking off restores any route beacon that is still
active; the two systems no longer overwrite one another's target.

## Game bugs worked around

These are pre-existing defects in the game, not accessibility features. They are here
because each one obstructs the work, and each is toggleable in config.

**Intro video deadlock** (`Video / WatchdogEnabled`). `MMVideoPlayer.Update` can reach
neither of its exits when a video fails to render its first frame: the completion check
requires `videoPlayer.frame > 0`, and the skip handler is nested inside
`if (videoPlayer.isPlaying)` so the skip input is never even read. Meanwhile `Play`
enables the "Skip" prompt unconditionally. The result is a black screen with a Skip
prompt that does nothing, forever — unrecoverable, and completely opaque without sight.
`errorReceived` does not fire for this, because a null clip from a failed
`Resources.Load` is silent. The watchdog detects a video producing no frames and drives
the game's own `EndReached` to continue startup. `Video / SkipAllVideos` bypasses videos
entirely, which is reasonable since they carry no accessible content.

**Startup NullReferenceException flood** (`Workarounds / SuppressSkeletonLodSpam`).
`SkeletonAnimationLODGlobalManager.Update` dereferences `MonoSingleton<UIManager>.Instance`
without a null check, and it begins updating before `UIManager` exists. Because
`MonoSingleton.Instance` falls back to `Object.FindObjectOfType<T>()` when null, each
frame in that window costs a scene-wide scan, a thrown exception, a stack trace written
to disk, and a Unity crash-report upload. It stops on its own once `UIManager` appears.
We skip that update until then; the method body is only LOD quality selection.

## Roadmap

- [x] BepInEx plugin, speech provider stack (NVDA / Tolk / SAPI), markup cleanup, hotkeys
- [x] Menu focus announcements with roles, states and positions
- [x] Verified in-game: NVDA speech audible, menu navigation reads correctly
- [x] Braille output alongside speech
- [x] Dialogue: speaker name, full line, choice summary, barks
- [x] Narration: full-screen quote cards, cutscene subtitles
- [x] Tutorial and popup body text
- [x] Walking guidance on the game's A* graph, with safe hotkeys verified against a
      runtime dump of the game's own bindings
- [x] Follower interaction wheel: entry context, live choice and modal input ownership
- [x] Objective HUD: new, updated, completed and failed objective announcements
- [x] Settings tabs and in-place slider, selector, and toggle change feedback
- [x] Twitch setup state, raffle/vote overlays, and core gameplay consequence announcements
- [x] Player health/fervour changes and on-demand status
- [x] Tarot reveal, choice, collection, reward, acquisition, and removal announcements
- [x] Inventory, quest log, rituals, sermon/weapon/curse wheels, doctrines, and upgrade trees
- [x] Base-building catalogue and live placement-grid announcements
- [ ] Base-building structure queries, inspection cursor and jump navigation
- [x] Audio beacons for navigation
- [x] Initial combat assistance: enemy radar/beacon plus dodge, wall, projectile, grenade,
      evade, combat-start, and room-clear feedback; runtime cue calibration is pending
- [x] Spoken in-game settings menu, with a switch and a volume for every individual cue
- [x] Learn sounds: a walkthrough that plays each cue, explains it, and demonstrates its
      directional encoding
- [x] Always-on proximity sounds for walls, items, interactables, characters, enemies and
      projectiles, off by default and individually configurable
- [x] Static-trap warning, and a cue vocabulary separated by material and rhythm rather than
      pitch alone
- [ ] Enemy-specific melee and beam coverage, and complete dungeon-combat coverage
- [ ] Hazard families beyond spike traps: lava, lightning, poison and the rest of the
      `Trap*` set

## Licensing / third-party

`lib/x64/nvdaControllerClient64.dll` is NV Access's NVDA controller client, obtained from
the [Tolk](https://github.com/dkager/tolk) distribution. Its licence is included as
`lib/LICENSE-NVDA.txt`.

`decomp/` (git-ignored) holds a decompilation of the installed game's assemblies, used
as reference only. It is not redistributable and must never be committed.
