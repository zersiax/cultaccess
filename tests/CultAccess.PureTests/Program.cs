using System;
using CultAccess.Combat;
using CultAccess.Localization;
using CultAccess.Navigation;
using CultAccess.Status;
using CultAccess.UI;
using CultAccess.Util;

internal static class Program
{
    private static int Main()
    {
        AssertClean("icon-only resource", "<sprite name=\"icon_wood\">", "Lumber");
        AssertClean(
            "resource noun carried by icon",
            "Chop <sprite name=\"icon_wood\">",
            "Chop Lumber");
        AssertClean(
            "duplicate resource icon",
            "Collect Lumber <sprite name=\"icon_wood\"> (5/5)",
            "Collect Lumber (5/5)");
        AssertClean(
            "duplicate gold icon and punctuation",
            "more gold <sprite name=\"icon_blackgold\"> .",
            "more gold.");
        AssertClean(
            "singular plural icon equivalence",
            "prepare meals <sprite name=\"icon_Meal\"> for Followers",
            "prepare meals for Followers");
        AssertClean(
            "decorative title sprites and shorthand colour",
            "<sprite name=\"img_Swirley_Left\"><#3C3FC5>The One Who Waits" +
            "<sprite name=\"The One\"><sprite name=\"img_Swirley_Right\">",
            "The One Who Waits");

        // The game separates two facts on one line with a pipe, and leaves one dangling where
        // an icon was stripped. A screen reader says "vertical bar" for both.
        AssertClean("dangling pipe before a comma", "Re-Assign |, gozer Lives Here",
            "Re-Assign, gozer Lives Here");
        AssertClean("pipe separating two facts", "Role: Devout Worker | Age: 20 Days",
            "Role: Devout Worker, Age: 20 Days");
        AssertClean("pipe inside brackets", "( Day 54 | 2 x Followers )",
            "( Day 54, 2 x Followers )");

        Assert(
            !RichText.IsUsableLocalization(
                "Structures/PLACEMENT_REGION", "Structures/PLACEMENT_REGION"),
            "untranslated localization term must be rejected");
        Assert(
            RichText.IsUsableLocalization("Build Area", "Structures/PLACEMENT_REGION"),
            "translated localization value must be accepted");
        Assert(
            !RichText.HasSemanticText("."),
            "an icon-only punctuation placeholder must not become a spoken object name");
        Assert(
            RichText.HasSemanticText("Blue Heart"),
            "a real pickup name must remain semantic text");

        // The game bakes icon-font characters straight into label text as Private Use Area
        // glyphs; FontImageNames.IconForCommand alone returns 49 of them. Nothing can say
        // them, so they must not survive cleaning.
        AssertClean("private use glyph removed", "Talk \uF075", "Talk");
        AssertClean(
            "private use glyph between words does not fuse them",
            "Give\uF4BDItem",
            "Give Item");
        AssertClean(
            "characters just outside the private use area are real text",
            "Level \uFF10",
            "Level \uFF10");

        // Some of the game's label data carries the same icons as the literal text of an
        // escape sequence rather than as the character, so there was nothing for the glyph
        // rule to match and a reader said "backslash u f one zero two" out loud.
        AssertClean(
            "icon written as literal escape text is removed",
            "Sinterklaas - Lvl I, \\uf102, \\ue074, Member for 18 days",
            "Sinterklaas - Lvl I, Member for 18 days");
        AssertClean(
            "an escape naming an ordinary character is left alone",
            "press \\u0041 to continue",
            "press \\u0041 to continue");
        AssertClean(
            "an escape just past the private use range is left alone",
            "value \\uf900",
            "value \\uf900");

        // The game's own icon table names every item, already localised. Registering from it
        // is what stops "25% less Fervour <blackSoul icon>" reading as "Fervour black Soul".
        RichText.RegisterSpriteWord("icon_blackSoul", "Fervour");
        AssertClean(
            "registered sprite duplicating nearby prose is dropped",
            "Curses consume 25% less Fervour <sprite name=\"icon_blackSoul\">",
            "Curses consume 25% less Fervour");
        RichText.RegisterSpriteWord("icon_DoctrineStone", "Doctrine Stone");
        AssertClean(
            "registered sprite carrying the only noun is spoken",
            "<sprite name=\"icon_DoctrineStone\">",
            "Doctrine Stone");
        Assert(
            RichText.SpriteWordCount >= 8,
            "registering must add to the baseline vocabulary rather than replace it");
        RichText.RegisterSpriteWord("icon_Nothing", "   ");
        AssertClean(
            "a sprite registered with no speakable word falls back to its name",
            "<sprite name=\"icon_Nothing\">",
            "Nothing");

        // The game reuses one sprite for more than one item: icon_wood is claimed by both LOG
        // and FORGE_FLAME. Registering in enum order let the second overwrite the first, and
        // lumber costs were read out as "Sacred Flame".
        RichText.RegisterSpriteWord("icon_wood", "Sacred Flame");
        AssertClean(
            "the game's table must not overwrite a confirmed baseline word",
            "<sprite name=\"icon_wood\">",
            "Lumber");

        // Between two entries from the game's own table there is no confirmed word, so the
        // first is kept rather than the last silently winning.
        RichText.RegisterSpriteWord("icon_Mushroom", "Small Mushroom");
        RichText.RegisterSpriteWord("icon_Mushroom", "Big Mushroom");
        AssertClean(
            "a second claim on the same sprite is refused, not applied",
            "<sprite name=\"icon_Mushroom\">",
            "Small Mushroom");
        Assert(
            RichText.CollisionCount >= 2,
            "refused registrations must be counted so a clash can be seen in the log");

        RichText.ResetSpriteWords();
        AssertClean(
            "resetting restores the baseline and drops registrations",
            "Curses consume 25% less Fervour <sprite name=\"icon_blackSoul\">",
            "Curses consume 25% less Fervour black Soul");
        AssertClean(
            "the baseline survives a reset",
            "<sprite name=\"icon_wood\">",
            "Lumber");

        // Every follower conversation in the game lost its speaker name to this. The game
        // wraps the literal name in a colour tag, and the term-path split then found the
        // slash inside the closing tag. Live logs showed the result as "color>".
        Assert(
            SpeakerNameText.FromRawCharacterName("<color=yellow>Argre</color>") == "Argre",
            "a colour-wrapped follower name must survive as the name, not as tag debris");
        Assert(
            SpeakerNameText.FromRawCharacterName("<color=yellow>PETERI</color>") == "PETERI",
            "stripping markup must not depend on the name's casing");
        Assert(
            SpeakerNameText.FromRawCharacterName("Ratau") == "Ratau",
            "a bare literal name must pass through unchanged");
        Assert(
            SpeakerNameText.FromRawCharacterName("NAMES/CultLeaders/Dungeon2") == "Dungeon2",
            "a genuine term path must still reduce to its last segment");
        Assert(
            SpeakerNameText.FromRawCharacterName("<color=yellow>NAMES/Foo/Bar</color>") == "Bar",
            "markup must come off before the path split, not after");
        Assert(
            SpeakerNameText.FromRawCharacterName(
                "<sprite name=\"img_SwirleyLeft\"> Narinder <sprite name=\"img_SwirleyRight\">") ==
            "Narinder",
            "decorative sprite flourishes around a name must not become part of it");
        Assert(
            SpeakerNameText.FromRawCharacterName("-") == null,
            "the game's unset marker must resolve to no speaker");
        Assert(
            SpeakerNameText.FromRawCharacterName("<color=yellow></color>") == null,
            "markup with no name inside it must resolve to no speaker, never to debris");
        Assert(
            !RichText.HasMarkupResidue(
                SpeakerNameText.FromRawCharacterName("<color=yellow>Argre</color>")),
            "a resolved speaker name must never trip the markup residue guard");

        Assert(
            DungeonDoorText.Requirement(1, 7) ==
            "requires 7 followers; you have 1",
            "dungeon door wording must speak the threshold before the current count");
        Assert(
            DungeonDoorText.Requirement(1, 1) ==
            "requires 1 follower; you have 1",
            "dungeon door wording must singularize a one-follower threshold");

        Assert(
            RoutePlanarMath.Distance(0.19f, 29.61f, -0.52f, 30.24f) < 1.25f,
            "DoorRoom's first waypoint must advance by floor distance despite render depth");

        Assert(
            RouteGuidanceText.Step(
                "south west", "2 metres", "Darkwood", "10 metres",
                finalStep: false, isTurn: false, isFirstInstruction: true) ==
            "Go south west for 2 metres. Darkwood, 10 metres remaining.",
            "the first route instruction must state an actionable heading and distance");
        Assert(
            RouteGuidanceText.Step(
                "south east", "3 metres", "Darkwood", "8 metres",
                finalStep: false, isTurn: true, isFirstInstruction: false) ==
            "Turn south east. Continue for 3 metres. Darkwood, 8 metres remaining.",
            "an actual heading change must state the new turn immediately");
        Assert(
            RouteGuidanceText.Step(
                "north", "4 metres", "Darkwood", "4 metres",
                finalStep: true, isTurn: true, isFirstInstruction: false) ==
            "Turn north. Continue for 4 metres to Darkwood.",
            "the final route segment must name its direction and destination");
        Assert(
            RouteGuidanceText.DirectLine("east", "Darkwood", "1.2 metres") ==
            "Continue east for 1.2 metres to Darkwood, direct line.",
            "direct-line fallback must remain a complete movement instruction");

        // Names heard in the 2026-08-23 base session, and what each was wrong about.
        Assert(
            RichText.HumaniseKey("COLLECTED_RESOURCES_CHEST") == "Collected Resources Chest",
            "a SCREAMING_SNAKE key must not reach the player still shouting");
        Assert(
            RichText.HumaniseKey("Hub1_Swamp") == "Hub1 Swamp",
            "an identifier that already has lower case must be left exactly as it is");
        Assert(
            RichText.Humanise("PETERI") == "PETERI",
            "plain Humanise must never re-case: a follower named in caps meant it");
        Assert(
            RichText.Humanise("BuildSite") == "Build Site",
            "camel case splitting must survive the title-casing change");

        Assert(
            !RichText.IsUsableLocalization(
                "COLLECTED_RESOURCES_CHEST", "Structures/COLLECTED_RESOURCES_CHEST"),
            "the term echoed back without its path is a missing translation, not a name");
        Assert(
            RichText.IsUsableLocalization("Meat", "Items/Meat"),
            "a real translation that happens to equal the last path segment must be kept");

        Assert(
            RichText.ContainsWord("Meal Bad Meat", "Meal"),
            "a label repeated as a whole word in the name is redundant");
        Assert(
            !RichText.ContainsWord("Cooking Fire", "Cook"),
            "a label that is merely a substring of the name is still the action");

        Assert(
            RichText.TrimTrailingPunctuation(
                "Followers deposit resources here while you are away.") ==
            "Followers deposit resources here while you are away",
            "a description used as a label must not bring its full stop into the sentence");
        Assert(
            RichText.TrimTrailingPunctuation("Assign |") == "Assign",
            "layout debris on the end of a label must not be spoken");
        Assert(
            RichText.TrimTrailingPunctuation("Receive Devotion 12 / 70") ==
            "Receive Devotion 12 / 70",
            "a label ending in a digit must be left alone");

        // Autowalk yields to the player between its own deadzone and the game's movement
        // threshold of 0.3. That band is the whole point: the player is pushing, the game is
        // not moving them yet, and driving through it would steer against them.
        Assert(
            !AutowalkPolicy.PlayerIsSteering(0f, 0f),
            "a stick at rest must not be read as the player taking over");
        Assert(
            !AutowalkPolicy.PlayerIsSteering(0.1f, -0.1f),
            "stick noise below the deadzone must not interrupt autowalk");
        Assert(
            AutowalkPolicy.PlayerIsSteering(0f, -0.25f),
            "input the game has not yet acted on must still hand control back");
        Assert(
            AutowalkPolicy.PlayerIsSteering(-1f, 0f),
            "a single axis held hard is steering even though the pair is short");

        var progress = new AutowalkProgress();
        progress.Reset(0f, 0f, 0f);
        Assert(
            !progress.Observe(AutowalkPolicy.StuckSeconds + 1f, 0f, 5f),
            "covering ground must never be reported as stuck, however long it took");
        Assert(
            !progress.Observe(AutowalkPolicy.StuckSeconds + 1.5f, 0f, 5f),
            "the clock must restart from the last real progress, not from engagement");
        Assert(
            progress.Observe(AutowalkPolicy.StuckSeconds * 2f + 1f, 0f, 5.1f),
            "a whole interval pinned against something must be reported");
        Assert(
            !progress.Observe(AutowalkPolicy.StuckSeconds * 2f + 1.1f, 0f, 5.1f),
            "one report per stuck interval, not one per frame");

        var fullHealth = new HealthSnapshot(6f, 6f, 0f, 0f, 0f, 0f, 0f, 0f);
        var damagedHealth = new HealthSnapshot(5f, 6f, 0f, 0f, 0f, 0f, 0f, 0f);
        Assert(
            PlayerStatusText.HealthChange(fullHealth, damagedHealth) ==
            "Health dropped to 2.5 of 3 hearts",
            "health loss must convert the game's two-points-per-heart units");

        var blueHeartHealth = new HealthSnapshot(6f, 6f, 0f, 0f, 2f, 0f, 0f, 0f);
        Assert(
            PlayerStatusText.HealthChange(fullHealth, blueHeartHealth) ==
            "Gained 1 blue heart. Health 4 of 4 hearts",
            "special-heart gains must identify the heart type and final total");
        Assert(
            PlayerStatusText.HealthPickup("Blue Heart", fullHealth, blueHeartHealth) ==
            "Picked up Blue Heart. Gained 1 blue heart. Health 4 of 4 hearts",
            "heart collection must correlate the pickup name with its resulting state");
        Assert(
            PlayerStatusText.HealthPickup("Red Heart", damagedHealth, fullHealth) ==
            "Picked up Red Heart. Health rose to 3 of 3 hearts",
            "ordinary healing pickups must still identify the collected item");
        Assert(
            PlayerStatusText.HealthStatus(damagedHealth) ==
            "Health 2.5 of 3 hearts. red 2.5 of 3",
            "requested health status must include current and capacity");

        Assert(
            PlayerStatusText.FervourChange(132f, 88f, 132f, 44f) ==
            "Fervour dropped to 67 percent, 2 of 3 curses ready",
            "fervour spending must report bar percentage and ready curse count");
        Assert(
            PlayerStatusText.FervourChange(88f, 132f, 132f, 44f) ==
            "Fervour full, 3 curses ready",
            "a refill must announce full fervour and ready curses");

        Assert(
            ProgressionText.Cost("Bone", 10, 7) == "cost 10 Bone, 7 owned",
            "ritual costs must distinguish the requirement from the owned amount");
        Assert(
            ProgressionText.Position("active quest", 1, 3) == "active quest 2 of 3",
            "progression positions must convert zero-based indices for speech");
        Assert(
            ProgressionText.ChooseThenConfirm("E", true, "declare") ==
            "press E to choose, then keep E held for 3 seconds to declare",
            "doctrine hold mode must explain both stages without implying stop-and-wait");
        Assert(
            ProgressionText.ChooseThenConfirm("E", false, "declare") ==
            "press E to choose, then press E again to declare",
            "doctrine no-hold mode must explain the second confirmation");
        Assert(
            ProgressionText.Confirm("E", false, "unlock") == "press E to unlock",
            "upgrade no-hold mode must not instruct a hold");

        AssertThreat(
            "head-on projectile",
            expected: true,
            playerX: 0f, playerY: 0f, playerVelocityX: 0f, playerVelocityY: 0f,
            threatX: 5f, threatY: 0f, threatVelocityX: -10f, threatVelocityY: 0f,
            combinedRadius: 0.75f, horizon: 1f,
            expectedTime: 0.5f);
        AssertThreat(
            "parallel near miss",
            expected: false,
            playerX: 0f, playerY: 0f, playerVelocityX: 0f, playerVelocityY: 0f,
            threatX: 5f, threatY: 2f, threatVelocityX: -10f, threatVelocityY: 0f,
            combinedRadius: 0.75f, horizon: 1f);
        AssertThreat(
            "projectile moving away",
            expected: false,
            playerX: 0f, playerY: 0f, playerVelocityX: 0f, playerVelocityY: 0f,
            threatX: 2f, threatY: 0f, threatVelocityX: 5f, threatVelocityY: 0f,
            combinedRadius: 0.75f, horizon: 1f);
        AssertThreat(
            "player walking into crossing shot",
            expected: true,
            playerX: 0f, playerY: 0f, playerVelocityX: 2f, playerVelocityY: 0f,
            threatX: 2f, threatY: 2f, threatVelocityX: 0f, threatVelocityY: -2f,
            combinedRadius: 0.75f, horizon: 1.5f,
            expectedTime: 1f);
        AssertThreat(
            "collision beyond warning horizon",
            expected: false,
            playerX: 0f, playerY: 0f, playerVelocityX: 0f, playerVelocityY: 0f,
            threatX: 10f, threatY: 0f, threatVelocityX: -2f, threatVelocityY: 0f,
            combinedRadius: 0.75f, horizon: 1f);

        Assert(
            OnboardingText.NextStep("Indoctrinate") ==
            "Next step, indoctrinate your new follower",
            "the indoctrinate phase must name the pending follower step");
        Assert(
            OnboardingText.NextStep("IndoctrinateBerriesAllowed") ==
            OnboardingText.NextStep("Indoctrinate"),
            "the berries variant asks for the same player action");
        Assert(
            OnboardingText.NextStep("Shrine") == "Next step, build a Shrine",
            "the shrine phase must use the game's own structure name");
        Assert(
            OnboardingText.NextStep("Shrine", hasPendingRecruit: true) ==
            "Next step, go to the indoctrination platform and indoctrinate your new follower",
            "a saved pending recruit must supersede the construction wording");
        Assert(
            OnboardingText.NextStep("Devotion") ==
            "Next step, command a follower to worship at the Shrine, then collect Devotion",
            "the devotion phase must include both the command and collection steps");
        Assert(
            OnboardingText.NextStep("Off").Length == 0 &&
            OnboardingText.NextStep("Done").Length == 0,
            "no step is pending before onboarding starts or after it finishes");
        Assert(
            OnboardingText.NextStep(null).Length == 0,
            "a missing phase must not produce speech");
        Assert(
            OnboardingText.NextStep("FeedTheCult") == "Next step, feed the cult",
            "an unknown future phase must still say something rather than go silent");

        Assert(
            BuildingEntranceText.Describe("Temple", open: true) ==
            "Temple, entrance, walk in without pressing Interact",
            "an open building entrance must say walking in is the action, not Interact");
        Assert(
            BuildingEntranceText.Describe("Temple", open: false) ==
            "Temple, entrance, blocked",
            "a blocked building entrance must not imply it can be entered");
        Assert(
            BuildingEntranceText.Arrival("Temple", open: true) ==
            "At Temple. Walk into the doorway to go inside.",
            "arriving at an open entrance must instruct walking through");

        Assert(
            DayCycleText.NewDay(12, new[] { "Sermon" }) == "Day 12. Sermon available.",
            "a new day must lead with the actions it restores");
        Assert(
            DayCycleText.NewDay(3, new string[0]) == "Day 3.",
            "a day that restores nothing must not invent an availability claim");
        Assert(
            DayCycleText.NewDay(3, null) == "Day 3.",
            "a missing restored-action list must not throw or speak rubbish");
        Assert(
            DayCycleText.Status(12, "Afternoon", "Dusk", 38.2f) ==
            "Day 12, Afternoon, about 38 seconds until Dusk",
            "the on-demand readout must name day, phase and time to the next phase");
        Assert(
            DayCycleText.Status(12, "Night", "", 0f) == "Day 12, Night",
            "an unknown next phase must drop the countdown rather than guess");
        Assert(
            DayCycleText.Status(1, "Dawn", "Morning", 1f) ==
            "Day 1, Dawn, about 1 second until Morning",
            "a one-second countdown must be singular");

        Assert(
            RichText.HasMarkupResidue("color>"),
            "leftover markup must be recognised so it is never spoken as a name");
        Assert(
            !RichText.HasMarkupResidue("towel") && !RichText.HasMarkupResidue(null),
            "an ordinary name must not be mistaken for markup debris");

        Assert(
            Strings.Get("day.nightfall") == "Night falls.",
            "a known key must resolve from the compiled defaults");
        Assert(
            Strings.Format("day.new", 12) == "Day 12.",
            "a template must substitute its placeholder");
        Assert(
            Strings.Get("no.such.key.exists") == "no.such.key.exists",
            "an unknown key must speak itself rather than fall silent");
        Assert(
            Strings.Plural("duration.second", "duration.seconds", 1) == "1 second" &&
            Strings.Plural("duration.second", "duration.seconds", 38) == "38 seconds",
            "plural selection must pick the right key for the count");


        // A walking instruction that points into something the wall cue is already reporting
        // must say so. The two subsystems were describing the same moment and contradicting
        // each other, and the words won because they sound more specific than a tone.
        Assert(
            RouteGuidanceText.Step(
                "north east", "1.6 metres", "Old Faith passage", "14 metres",
                finalStep: false, isTurn: false, isFirstInstruction: false,
                blockedAhead: true) ==
            "Continue north east for 1.6 metres. Old Faith passage, 14 metres remaining. " +
            "Blocked that way.",
            "an instruction pointing into a wall the sonar has hit must say it is blocked");
        Assert(
            !RouteGuidanceText.Step(
                "north east", "1.6 metres", "Old Faith passage", "14 metres",
                finalStep: false, isTurn: false, isFirstInstruction: false).Contains("Blocked"),
            "an unobstructed instruction is unchanged");
        Assert(
            RouteGuidanceText.DirectLine("north", "the door", "2 metres", blockedAhead: true)
                .EndsWith("Blocked that way."),
            "the final direct approach carries the warning too, which is where a doorway is");

        AssertLocalizationFiles();
        AssertCultStatus();
        AssertFollowerStatus();

        AudioTests.Run(Assert);
        ConfigMenuTests.Run(Assert);

        Console.WriteLine("CultAccess pure tests passed.");
        return 0;
    }

    private static void AssertClean(string name, string raw, string expected)
    {
        var actual = RichText.Clean(raw);
        Assert(actual == expected, $"{name}: expected '{expected}', got '{actual}'");
    }

    private static void AssertThreat(
        string name,
        bool expected,
        float playerX,
        float playerY,
        float playerVelocityX,
        float playerVelocityY,
        float threatX,
        float threatY,
        float threatVelocityX,
        float threatVelocityY,
        float combinedRadius,
        float horizon,
        float? expectedTime = null)
    {
        var actual = ThreatPrediction.TryPredict(
            playerX, playerY, playerVelocityX, playerVelocityY,
            threatX, threatY, threatVelocityX, threatVelocityY,
            combinedRadius, horizon, out var time, out _);
        Assert(actual == expected, $"{name}: expected threat={expected}, got {actual}");
        if (expectedTime.HasValue)
            Assert(
                Math.Abs(time - expectedTime.Value) < 0.001f,
                $"{name}: expected t={expectedTime.Value}, got {time}");
    }

    /// <summary>
    /// Exercises the catalogue loader against a real file: overrides, the line-break escape,
    /// metadata, and coverage counting. The escape in particular shipped broken once because
    /// a tooling quirk ate a backslash, and nothing was watching it.
    /// </summary>
    private static void AssertLocalizationFiles()
    {
        var root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "cultaccess-loc-" + System.Guid.NewGuid().ToString("N"));
        var lang = System.IO.Path.Combine(root, "lang");
        System.IO.Directory.CreateDirectory(lang);
        try
        {
            System.IO.File.WriteAllLines(
                System.IO.Path.Combine(lang, "xx.txt"),
                new[]
                {
                    "# a comment",
                    "_meta.status = human-reviewed",
                    "day.nightfall = Nacht faellt.",
                    "day.new = Tag {0}.\\nDone",
                });

            Strings.Initialize(root, "xx");

            Assert(
                Strings.Get("day.nightfall") == "Nacht faellt.",
                "an override must win over the compiled default");
            Assert(
                Strings.Format("day.new", 3) == "Tag 3." + "\n" + "Done",
                "a literal backslash-n in a language file must become a real line break");
            Assert(
                Strings.MetaValue("_meta.status") == "human-reviewed",
                "declared provenance must be readable");
            Assert(
                Strings.Get("day.status") == "Day {0}, {1}",
                "an untranslated key must fall back to English, not vanish");

            var missing = Strings.UntranslatedKeys();
            Assert(
                !missing.Contains("day.nightfall") && missing.Contains("day.status"),
                "coverage must list exactly the keys this language does not translate");
            Assert(
                !missing.Contains("_meta.status"),
                "metadata must not be counted as an untranslated line");
        }
        finally
        {
            Strings.Initialize(null, "en");
            try { System.IO.Directory.Delete(root, true); } catch { }
        }
    }

    /// <summary>
    /// The cult bars. What is worth pinning is not the arithmetic but the policy: a bar the
    /// player has not unlocked is silent, a frozen bar is never a warning, and the crossing
    /// sentence names the consequence the game's own simulation will produce.
    /// </summary>
    private static void AssertCultStatus()
    {
        var healthy = Bar(CultBarKind.Faith, 0.6f);
        var low = Bar(CultBarKind.Faith, 0.2f);
        var hidden = new CultBar(CultBarKind.Warmth, 0.1f, shown: false, locked: false);
        var locked = new CultBar(CultBarKind.Food, 0.1f, shown: true, locked: true);

        Assert(!healthy.Low, "a bar above a quarter is not low");
        Assert(low.Low, "a bar below a quarter is low");
        Assert(!hidden.Low, "a bar the player has not unlocked cannot be low");
        Assert(
            !locked.Low,
            "a frozen bar must never read as a warning: it cannot move, so nothing is coming");

        var snapshot = new CultStatusSnapshot(
            low, Bar(CultBarKind.Food, 0.8f), Bar(CultBarKind.Cleanliness, 0.5f), hidden,
            followers: 7, dead: 2);
        var status = CultStatusText.Status(snapshot);
        Assert(
            status == "Faith 20 percent. Food 80 percent. Cleanliness 50 percent. 7 followers, 2 dead.",
            "the full readout must skip a locked-out bar and name the population: " + status);

        var oneAlive = new CultStatusSnapshot(
            healthy, hidden, hidden, hidden, followers: 1, dead: 0);
        Assert(
            CultStatusText.Status(oneAlive) == "Faith 60 percent. 1 follower.",
            "a single follower must not be pluralised and no dead count is spoken at zero");

        Assert(
            CultStatusText.Status(new CultStatusSnapshot(
                hidden, hidden, hidden, hidden, 0, 0)) == "The cult has no bars yet.",
            "a cult with nothing revealed must say so rather than read an empty sentence");

        Assert(
            CultStatusText.Alerts(snapshot) == "Faith low",
            "the where-am-I clause names only the bars that are actually low");
        Assert(
            CultStatusText.Alerts(oneAlive).Length == 0,
            "the where-am-I clause must add nothing at all when every bar is healthy");
        Assert(
            CultStatusText.Alerts(new CultStatusSnapshot(
                low, Bar(CultBarKind.Food, 0.1f), Bar(CultBarKind.Cleanliness, 0.9f), hidden,
                3, 0)) == "Faith, Food low",
            "several low bars are named in one clause");

        Assert(
            CultStatusText.Crossing(CultBarKind.Faith, 0.2f, low: true) ==
            "Faith low, 20 percent. Followers will start to dissent.",
            "a crossing must name what the game will now start doing");
        Assert(
            CultStatusText.Crossing(CultBarKind.Cleanliness, 0.19f, low: true) ==
            "Cleanliness low, 19 percent. Followers will start to fall ill.",
            "each bar names its own consequence");
        Assert(
            CultStatusText.Crossing(CultBarKind.Warmth, 0.2f, low: true) == "Warmth low, 20 percent.",
            "warmth claims no consequence, because its own simulation step does nothing");
        Assert(
            CultStatusText.Crossing(CultBarKind.Faith, 0.31f, low: false) ==
            "Faith back up, 31 percent.",
            "recovery is reported plainly");

        Assert(
            CultStatusText.LockChanged(CultBarKind.Faith, locked: true) ==
            "Faith is locked and cannot change.",
            "a frozen bar is reported as frozen");

        // The underlying floats overshoot their own limits slightly; a bar reading 101 or
        // minus 1 percent would sound like a defect rather than a full or empty one.
        Assert(CultStatusText.Percent(1.004f) == 100, "percentages clamp at 100");
        Assert(CultStatusText.Percent(-0.02f) == 0, "percentages clamp at 0");
    }

    private static CultBar Bar(CultBarKind kind, float value) =>
        new CultBar(kind, value, shown: true, locked: false);

    /// <summary>
    /// One follower. The assertions that matter are the hidden bars: the game itself does not
    /// show loyalty for a mutated follower, or food and health for a dead one, and speaking
    /// them would be more than equal access rather than less.
    /// </summary>
    private static void AssertFollowerStatus()
    {
        // Identity in a list. The species is what a sighted player picks a follower out by
        // from across the base, and level 1 is left unsaid because everyone starts there and
        // it would put the same three words in front of every entry.
        Assert(
            FollowerStatusText.Identity("Sinterklaas", "Goat", 4) == "Sinterklaas the level 4 Goat",
            "a levelled follower is named, levelled and formed in one phrase");
        Assert(
            FollowerStatusText.Identity("Sinterklaas", "Goat", 1) == "Sinterklaas the Goat",
            "level 1 is not spoken, because it distinguishes nobody");
        Assert(
            FollowerStatusText.Identity("Sinterklaas", "", 4) == "Sinterklaas, level 4",
            "an unnamed form falls back to name and level rather than a dangling article");
        Assert(
            FollowerStatusText.Identity("Sinterklaas", "", 1) == "Sinterklaas",
            "with neither form nor level there is nothing to add to the name");
        Assert(
            FollowerStatusText.Identity("", "Goat", 4) == "Follower details unavailable",
            "a follower with no name says so rather than reading as a bare form");

        Assert(
            FollowerStatusText.TargetEntry(
                "Sinterklaas the level 4 Goat", "Ill", "quest to complete", detailed: true) ==
            "Sinterklaas the level 4 Goat, Ill, quest to complete",
            "a list entry reads identity, then condition, then what to do about it");
        Assert(
            FollowerStatusText.TargetEntry(
                "Sinterklaas", "Ill", "quest to complete", detailed: false) == "Sinterklaas",
            "at low verbosity a list entry carries neither condition nor headline");
        Assert(
            FollowerStatusText.TargetEntry("Towel", "", "", detailed: true) == "Towel",
            "a follower with nothing wrong and nothing to offer adds no empty clauses");

        var full = new FollowerSnapshot
        {
            Name = "Sinterklaas",
            Species = "Goat",
            Role = "Farmer",
            Level = 3,
            Headline = "quest to complete",
            Condition = "Ill",
            BiggestNeed = "Nowhere to sleep",
            Task = "chop trees",
            LoyaltyShown = true, Loyalty = 40,
            NeedsShown = true, Food = 72, Health = 100,
            TraitCount = 2,
            Disciple = true,
            MarriedToLeader = true,
            Age = 12,
            MemberDays = 18,
        };

        Assert(
            FollowerStatusText.Card(full, detailed: false) == "Sinterklaas the level 3 Goat",
            "a low-verbosity tile is the identity and nothing else");
        Assert(
            FollowerStatusText.Card(full, detailed: true) ==
            "Sinterklaas the level 3 Goat, Farmer, Ill, loyalty 40 percent, food 72 percent, " +
            "health 100 percent",
            "a tile leads with the name and then reads the bars a sighted player sees beside it");
        Assert(
            FollowerStatusText.Detail(full) ==
            "Sinterklaas the level 3 Goat, Farmer, Ill, loyalty 40 percent, food 72 percent, " +
            "health 100 percent, quest to complete, needs Nowhere to sleep, doing chop trees, " +
            "2 traits, disciple, married to you, age 12, in the cult 18 days",
            "the on-demand reading adds the headline, the need, the task and the relationships");

        var dead = new FollowerSnapshot
        {
            Name = "Towel",
            Alive = false,
            LoyaltyShown = true, Loyalty = 100,
            NeedsShown = false, Food = 50, Health = 50,
            MemberDays = 0,
        };
        var deadText = FollowerStatusText.Detail(dead);
        Assert(
            deadText.Contains("dead") && deadText.Contains("loyalty 100 percent"),
            "a dead follower keeps the one bar their card still shows");
        Assert(
            !deadText.Contains("food") && !deadText.Contains("health"),
            "a dead follower's card hides food and health, so neither may be spoken");
        Assert(
            deadText.Contains("new to the cult"),
            "a follower who joined today is described as new rather than as zero days");

        var mutated = new FollowerSnapshot
        {
            Name = "Peteri",
            LoyaltyShown = false, Loyalty = 90,
            NeedsShown = true, Food = 30, Health = 80,
            Unavailable = "unavailable, dissenting",
            MemberDays = 1,
        };
        var mutatedText = FollowerStatusText.Card(mutated, detailed: true);
        Assert(
            !mutatedText.Contains("loyalty"),
            "loyalty is hidden for a mutated follower on screen and must be hidden in speech");
        Assert(
            mutatedText.StartsWith("Peteri, unavailable, dissenting"),
            "the reason a follower cannot be chosen follows the name immediately, " +
            "because it is what the player is deciding on: " + mutatedText);
        Assert(
            FollowerStatusText.Detail(mutated).Contains("in the cult 1 day"),
            "a one-day membership is not pluralised");

        Assert(
            FollowerStatusText.Card(null, detailed: true) == "Follower details unavailable" &&
            FollowerStatusText.Detail(new FollowerSnapshot()) == "Follower details unavailable",
            "an unreadable follower says so rather than reading as an empty line");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
