CultAccess - replacing the mod's sounds
=======================================

Every sound CultAccess makes is generated in code, so the mod works with this folder empty.
Drop a file in here named after a cue and it is used instead, with no rebuild and no change
to any setting.

This exists because which sounds are legible is genuinely personal. It depends on your
screen reader, your headphones, your hearing, and what else you have already trained
yourself to listen for. If a cue is unclear, fatiguing, or too close to something else, you
can replace that one sound and leave the rest alone.

How
---

1. Name the file after the cue, plus .wav, .ogg or .mp3. For example:
       static-trap.wav
2. Put it in this folder.
3. Restart the game.

The mod's settings menu, under Learn sounds, says the file name for whichever cue you are
on, so you never have to guess. It also plays the sound, which is the quickest way to decide
whether you want to replace it.

If a file is present but cannot be loaded, the log says so by name and the built-in sound is
used. It never fails silently.

The names
---------

Wayfinding. The beacon has three fixed notes rather than a sliding pitch, because a note
that slides can only tell you it changed, not where something is:
    beacon-ahead            target is up-screen, which is north, which is W
    beacon-side             target is off to one side; panning says which
    beacon-behind           target is down-screen, which is south, which is S

Combat:
    wall-ahead              a wall in the direction you are moving
    wall-contact            you have walked into something solid
    dodge-direction         which way a dodge is taking you
    dodge-into-wall         that dodge runs into something solid
    dodge-avoided-hit       a dodge just saved you from damage
    melee-windup            an enemy is winding up a melee attack
    incoming-shot           a shot is predicted to hit you
    danger-area             you are standing in a grenade landing area
    static-trap             a trap has triggered under you

Minigames:
    timing-window           held open for as long as a success window lasts; must loop
    timing-edge             a short chirp at the edge of a timing window

Always-on proximity sounds:
    near-item               dropped items and hearts
    near-interactable       things you can press Interact on
    near-character          followers and other characters
    near-enemy              living hostiles
    near-projectile         hostile shots in the air

Wall tones. These are continuous, so they must loop cleanly, and each direction is a
separate file because direction is carried by the note as much as by the stereo position:

    wall-north              wall-northeast          wall-east
    wall-southeast          wall-south              wall-southwest
    wall-west               wall-northwest

Notes on making your own
------------------------

- Mono is recommended. The mod sets the stereo position itself, and a file that is already
  panned will fight it.
- Keep them short. Most cues fire in the middle of combat, and anything longer than about a
  quarter of a second arrives after the thing it is warning about.
- timing-window and the wall tones are held open rather than played once, so they must loop
  seamlessly. A loop whose ends do not meet clicks on every repeat, several times a second.
- Do not normalise to full scale. The mod applies your volume setting on top, and the cues
  are deliberately balanced against each other; a file mastered much louder than the rest
  will unbalance the set.
