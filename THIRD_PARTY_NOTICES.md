# Third-party notices

CultAccess itself is MIT licensed; see `LICENSE`. This file covers everything else that
ships in, or is required by, the distributed package.

---

## Bundled: NVDA Controller Client

- File: `nvdaControllerClient64.dll`
- Source: the Tolk distribution, obtained from the Tolk source archive via
  `codeload.github.com`
- Upstream: part of NVDA (NonVisual Desktop Access), NV Access
- Licence: **GNU Lesser General Public License, version 2.1.** Full text ships alongside the
  library as `LICENSE-NVDA.txt` and is installed next to the plugin.

### Why this is bundled — a deliberate decision, not an oversight

The governing ruleset requires that bundling a screen reader bridge be an explicit,
documented choice, because authors legitimately differ on it.

The decision is to bundle, for one reason: **NVDA is the target user's screen reader, and
this mod exists specifically to be usable by blind players.** Requiring a separate manual
download and correct placement of a DLL is a real barrier for exactly the audience the mod
serves, and getting it wrong produces silence, which is indistinguishable from a broken
install to someone who cannot see the screen.

LGPL 2.1 permits redistribution provided the licence text accompanies the library and users
can replace it. Both hold here: the notice ships with the file, and the library is loaded by
absolute path from the plugin directory, so a user may substitute their own build by
replacing the file.

CultAccess does not modify the library.

---

## Required, not bundled: BepInEx

- Role: mod loader, BepInEx 5 for Unity Mono
- Obtained from Thunderstore
- Licence: LGPL 2.1
- **Not redistributed.** The user installs BepInEx themselves. The ruleset forbids shipping
  the mod loader, and the game is patched in place inside the Steam install.

Harmony (`0Harmony.dll`) ships as part of BepInEx and is likewise not redistributed by us.
Harmony is MIT licensed.

---

## Referenced, never redistributed: Cult of the Lamb

The build references assemblies from the installed game directly rather than through NuGet,
which is what makes offline builds possible:

- `Assembly-CSharp`, the game's own code
- Unity engine modules
- FMOD Studio Unity integration
- Rewired (Guavaman Enterprises), the game's input system
- A* Pathfinding Project (Aron Granberg), the game's navigation graph
- Spine Runtimes (Esoteric Software), character animation
- Odin Serializer / Sirenix

**None of these are copied into the build output or the distributed package.** The project
file sets `<Private>false</Private>` on every game reference specifically to guarantee this.
They are resolved at runtime from the user's own legally obtained installation.

The `decomp/` directory contains a decompilation of the user's own game install. It is
git-ignored and must never be published or redistributed.

---

## Speech backends not currently bundled

- **Tolk** — supported as a fallback path if a user supplies their own build. LGPL 3.0.
- **SAPI** — part of Windows; nothing to redistribute.
