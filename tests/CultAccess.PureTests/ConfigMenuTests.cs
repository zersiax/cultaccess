using System;
using CultAccess.UI;

/// <summary>
/// Cursor movement, value editing and wording for the mod's settings menu.
///
/// This is the screen a player uses while they are still learning what the mod does, and
/// every one of its behaviours is invisible: whether a value actually changed, whether the
/// cursor wrapped, whether a row read back what it is really set to. None of that can be
/// checked by looking, so it is checked here.
/// </summary>
internal static class ConfigMenuTests
{
    internal static void Run(Action<bool, string> assert)
    {
        AssertMovement(assert);
        AssertToggleIsDirectional(assert);
        AssertSliderClamps(assert);
        AssertChoiceWraps(assert);
        AssertSubmenuNavigation(assert);
        AssertWording(assert);
    }

    private static void AssertMovement(Action<bool, string> assert)
    {
        var page = Page(3);
        var navigator = new ConfigMenuNavigator();
        navigator.Open(page);

        assert(navigator.Page == page, "opening the menu must land on the page given");
        assert(page.Index == 0, "a freshly opened page must start on its first row");

        navigator.Move(1);
        assert(page.Index == 1, "down must advance the cursor");

        navigator.Move(-1);
        navigator.Move(-1);
        assert(
            page.Index == 2,
            "moving up from the first row must wrap to the last; a list read aloud has no " +
            "visible extent, so an invisible wall is worse than wrapping");

        navigator.Move(1);
        assert(page.Index == 0, "moving down from the last row must wrap to the first");

        assert(navigator.MoveTo(2) && page.Index == 2, "jumping to a valid row must work");
        assert(!navigator.MoveTo(9), "jumping past the end must be refused, not clamped");
    }

    /// <summary>
    /// Left is off and right is on. A directional key that flipped the value instead would
    /// make the resulting state depend on how many times it was pressed, which is exactly
    /// what a player who cannot see the row must not have to keep count of.
    /// </summary>
    private static void AssertToggleIsDirectional(Action<bool, string> assert)
    {
        var value = false;
        var page = new ConfigPage("Sounds");
        page.Add(new ConfigItem
        {
            Label = "Wall tones",
            Kind = ConfigItemKind.Toggle,
            GetToggle = () => value,
            SetToggle = set => value = set,
        });

        var navigator = new ConfigMenuNavigator();
        navigator.Open(page);

        assert(navigator.Adjust(1) && value, "right must switch a toggle on");
        assert(!navigator.Adjust(1), "right on an already-on toggle must report no change");
        assert(value, "right must never switch a toggle off");

        assert(navigator.Adjust(-1) && !value, "left must switch a toggle off");
        assert(!navigator.Adjust(-1), "left on an already-off toggle must report no change");

        assert(
            navigator.Activate() == ConfigActivation.Changed && value,
            "Enter must flip a toggle, which is the one place flipping is unambiguous");
    }

    private static void AssertSliderClamps(Action<bool, string> assert)
    {
        var value = 0.5f;
        var page = new ConfigPage("Sounds");
        page.Add(new ConfigItem
        {
            Label = "Volume",
            Kind = ConfigItemKind.Slider,
            GetValue = () => value,
            SetValue = set => value = set,
            Minimum = 0f,
            Maximum = 1f,
            Step = 0.05f,
            FormatValue = ConfigMenuText.Percent,
        });

        var navigator = new ConfigMenuNavigator();
        navigator.Open(page);

        assert(navigator.Adjust(1), "right must raise a slider");
        assert(Math.Abs(value - 0.55f) < 0.0001f, "a slider must move by exactly one step");

        for (var i = 0; i < 50; i++) navigator.Adjust(1);
        assert(Math.Abs(value - 1f) < 0.0001f, "a slider must stop at its maximum");
        assert(
            !navigator.Adjust(1),
            "a slider already at its maximum must report no change, so the menu can say the " +
            "value again rather than implying something happened");

        for (var i = 0; i < 50; i++) navigator.Adjust(-1);
        assert(Math.Abs(value) < 0.0001f, "a slider must stop at its minimum");
        assert(!navigator.Adjust(-1), "a slider already at its minimum must report no change");
    }

    private static void AssertChoiceWraps(Action<bool, string> assert)
    {
        var index = 0;
        var page = new ConfigPage("Wayfinding");
        page.Add(new ConfigItem
        {
            Label = "Guidance",
            Kind = ConfigItemKind.Choice,
            GetChoice = () => index,
            SetChoice = set => index = set,
            Choices = new[] { "beacon and speech", "beacon only", "speech only" },
        });

        var navigator = new ConfigMenuNavigator();
        navigator.Open(page);

        navigator.Adjust(1);
        navigator.Adjust(1);
        assert(index == 2, "right must step through the choices");

        navigator.Adjust(1);
        assert(index == 0, "a choice list must wrap round rather than stop");

        navigator.Adjust(-1);
        assert(index == 2, "left from the first choice must wrap to the last");
    }

    private static void AssertSubmenuNavigation(Action<bool, string> assert)
    {
        var child = Page(2);
        child.Title = "Combat cues";

        var root = new ConfigPage("CultAccess settings");
        root.Add(new ConfigItem
        {
            Label = "Combat cues",
            Kind = ConfigItemKind.Submenu,
            Submenu = child,
        });

        var navigator = new ConfigMenuNavigator();
        navigator.Open(root);

        assert(
            navigator.Activate() == ConfigActivation.EnteredSubmenu && navigator.Page == child,
            "Enter on a submenu row must enter it");

        navigator.Move(1);
        assert(navigator.Back() && navigator.Page == root, "Backspace must leave a submenu");
        assert(
            !navigator.Back(),
            "Backspace at the top must report that there is nowhere to go, so the caller can " +
            "close the menu instead of trapping the player in it");

        navigator.Activate();
        assert(
            child.Index == 1,
            "re-entering a page must return to the row that was focused, not reset to the top");
    }

    private static void AssertWording(Action<bool, string> assert)
    {
        var enabled = true;
        var suppressed = false;
        var page = new ConfigPage("Combat cues");
        page.Add(new ConfigItem
        {
            Label = "Static trap",
            Description = "A metallic rattle when a trap has triggered under you.",
            Kind = ConfigItemKind.Toggle,
            GetToggle = () => enabled,
            SetToggle = set => enabled = set,
            Suppressed = () => suppressed,
        });
        page.Add(new ConfigItem { Label = "Volume", Kind = ConfigItemKind.Submenu });

        assert(
            ConfigMenuText.Focus(page) == "Static trap, on, item 1 of 2",
            "a row must read as label, then value, then position: what it is, what it is set " +
            $"to, where it is (got \"{ConfigMenuText.Focus(page)}\")");

        enabled = false;
        assert(
            ConfigMenuText.Changed(page.Current) == "Static trap, off",
            "a changed row must say only the label and the new value");

        enabled = true;
        suppressed = true;
        assert(
            ConfigMenuText.Value(page.Current).Contains("currently silent"),
            "a cue switched on but silenced by a switch above it must say so, or the player " +
            "has no way to discover why it makes no sound");

        assert(
            ConfigMenuText.PageEntry(page).StartsWith("Combat cues. 2 items."),
            "entering a page must name it and say how big it is before reading a row");

        assert(
            ConfigMenuText.Percent(0.55f) == "55 percent",
            "a volume must be spoken as a whole percentage");
        assert(
            ConfigMenuText.Percent(0f) == "0 percent" && ConfigMenuText.Percent(1f) == "100 percent",
            "the ends of a volume range must be spoken exactly");
        assert(
            ConfigMenuText.Metres(5f) == "5 metres",
            "a range must be spoken in the same unit as spoken distances elsewhere");
        assert(
            ConfigMenuText.Seconds(0.25f) == "0.3 seconds",
            "a sub-second interval must keep a decimal rather than rounding away to zero");

        assert(
            ConfigMenuText.Detail(page.Items[1]).Length > 0,
            "a row with no explanation must still answer the help key rather than fall silent");
    }

    private static ConfigPage Page(int rows)
    {
        var page = new ConfigPage("Test page");
        for (var i = 0; i < rows; i++)
            page.Add(new ConfigItem { Label = "Row " + i, Kind = ConfigItemKind.Submenu });

        return page;
    }
}
