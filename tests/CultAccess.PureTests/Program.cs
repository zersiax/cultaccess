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

        AssertLocalizationFiles();

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

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
