# Hooman Changelog

What is this world coming to that we split these up. Anyway:

## 0.2.2

### Your cult, and your followers. Two new keys.

#### F3: how your cult is doing

* The game draws four bars across the top of the screen and none of them has a single word of text on it. **F3 reads all four**: faith, food, cleanliness and warmth, then how many followers you have and how many have died. They all read the same way round, so a big number is always good. Some are unlockable, those are ignored until unlocked.
Other players call these the faith, hunger and sickness bars. I named them for what a full bar means instead, because "hunger 80 percent" could just as easily mean everyone is starving.
If these bars get into "oh no bad" territory, notifications will start firing about consequences, but having this key is just an overall useful addition to actually get a clear idea of what's happening with these. Consequences include dissent, starving followers and puking in the streets. bad times.
* Faith notifications now tell you the number of faith up and down, as well as the reason why.



#### F4: one follower

\-F4 describes a single follower in full.  loyalty, food and health, what is wrong with them, what they are doing right now, their traits, who they are married to, their age and how long they have been with you. It describes whoever is selected in the target list, so focus  one with the followers pathfinder first. If your selection is not a follower it falls back to the nearest one.

* Every follower tile in the game now reads its bars. The roster, choosing a sacrifice, assigning someone to the daycare, the mating tent, beds, the healing bay, knucklebones, the confession booth. If I missed any, come yell at me.
* When you're picking a follower for something, and you can't pick that follower, you now get told why.



### Followers in the target list, and followers who come find you

* When going through targets in the follower pathfinder list you now get their shape, level, prominent things happening to them, etc., so you can decide to take action. Or not. Let the bastards suffer is a valid strat.
* There's a little speech bubble that pops over a follower's head at times when they want something. This mechanism is also used for the Twitch integration. SHOULD, keyword should, read now when that happens. Same goes for when they actively come find you. That thing gets refreshed every few seconds, i put a pretty strict gate on it so we'll have to see if we outright miss things the way they are now.

### Stats. More stats. All the stats. Seriously too many stats.

* The Cult page behind the Temple altar — the one with the View Followers button on it — has nine statistics on it: how many followers you have ever had, how many were murdered, starved, sacrificed, died of old age, how many crusades you have run, how many times you have died, how many enemies you have killed, how many winters you have survived. These now read as they should when the panels are opened. Same goes for the followers button in the main menu.



### The Read Mind screen

This basically already worked, but now gives you more details on what a "thought row is actually doing faith and loyalty-wise.

### Other stuff

* The bleepers for items and the pathfinder were having a fight and weren't talking. I paid for marriage counseling with my Patreon earnings and they now agree on the fact that when beeping for an item that can be picked up, it should probably also be something to guide towards. Fixed.



* The pathfinder also had a beef with the wall bumper, there were cases where it would confidently smash you straight into a wall. Not outright fixed, but should be improved.
* f7 has been improved to include some more animation-related things. If you're curious about what is happening around you, the visual animation description pass is coming up, log the occurrence and throw it my way.



# 0.2.1

This is where we make assorted combat and other fixes.

* Chains breaking on a door are now announced when it happens.
* Newly unlocked locations on the map should now be announced when it happens.
* There were a few instances of pathfinding needing a manual press of backslash to repopulate. I think I caught all of them.



* Temple boss doors now indicate they should be walked through rather than calling them unavailable.





# 0.2.0

## Controller hotkeys

* Hold the left trigger and the pad becomes the mod's hotkeys. I dumped the game's own controller bindings to find out what was free, and the answer was: the left trigger, and nothing else. Every other button on the pad already does three or four things.
* D-pad controls pathfinder. A starts and stops guidance. B lists enemies, X points the beacon at the next one, Y is where am I (f12). Left shoulder re-scans, right shoulder repeats the direction. Left stick click is autowalk, right stick click shuts speech up, right trigger repeats the last thing said. Back is help, Start is the settings menu.
* When a fight starts the pathfinder filter moves to the enemies on its own, and moves back when the room is clear.  Landing on an enemy points the beacon at it, so stepping and locking are one press instead of two. You can still switch to any other filter mid-fight.
* All of it is rebindable in the ControllerLayer section of the config file. An in-game page for it will come once you have played the defaults and know which ones you actually want moved.

## Other Stuff:

* Spawners now indicate they're spawners, and move to the top of the enemy list so you can get to them quicker.
* Enemies that jump around, get up and get down, would at times confuse the beacon. Should be better now.



* Melee warnings for two more enemies. The Forest Flying Worm and the diving maggot miniboss both give you more clear audio indicators of when you're about to get wrecked.
* In bossfights with more than one stage, the mod was a little enthusiastic about indicating the room was clear. Told off and docked pay, should behave better now.
* I'm tweaking the way health loss gets reported a bit to hopefully not talk over other things too much, let me know how it goes!



## Autowalk

* Not a huge fan of it but the autowalk feature is now a thing. Delete for keyboard autowalks to currently focused pathfinder target. I find it primarily helps with really narrow passages, as we use 8 directions for instructions and at times you need slightly more than that.

## 0.1.1: Installer hotfix

* The installer would not start if you unpacked it somewhere with a space in the path, like C:\\games\\cult of the lamb. Fixed.
* Some antivirus products flag the BepInEx download. There is now a second button in the installer, "Use a BepInEx zip I downloaded myself", which hands you the current link, waits while you fetch it however you like, and then lets you pick the file. Nothing is wrong with BepInEx; a mod loader patches a game, which looks the same as something unwanted doing it.

## 0.1.0: Initial release

* A lot of menus already read: title, pause, follower indoctrination, tarrot cards, temple, rituals, upgrade trees for divine inspiration, building screen, cooking screen, travel map, etc.
I am sure there are windows that don't read yet, but as I unlock them, I add support for them If thers unlock before me, feel free to send me logs and I'll see what I can do based on those.
* Audio cues: combat cues, wall tones, cues for enemies, items, interactables and projectiles all configurable.
* Pathfinder: audio beacon, speech instructions, both.
* Combat: Cues for melee wind-ups for quite a few enemies already, projectile indicators, trap and damage area indicators, successful dodge indicator.
* Info: f12 is a catch-all for time of day and character status right now,  will likely split up down the line. Important prompting events, interaction prompts etc. speak automatically.
* Building: Info on what buildings are unlocked, what places are safe to build on. f5 tells you which way the nearest free tile is.
* Followers: Indoctrinate and command followers, re-educate dissenters, perform quests for followers (announcements need a bit of tightening here and there but work)

