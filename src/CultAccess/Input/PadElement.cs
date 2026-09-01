namespace CultAccess.Input
{
    /// <summary>
    /// The controller elements a mod command can be bound to.
    ///
    /// Named for an XInput pad because that is what the binding probe was run against and what
    /// the defaults are chosen for. Resolution is by element *name* first and by the XInput
    /// element id only as a fallback, so a pad that labels its face buttons differently still
    /// works as long as Rewired reports recognisable names; what resolved is written to the
    /// log either way.
    ///
    /// <see cref="LeftTrigger"/> is deliberately present but must not be bound to a command:
    /// it is the layer modifier itself. The binding loader refuses it rather than producing a
    /// command that can only fire while it is holding itself down.
    /// </summary>
    internal enum PadElement
    {
        /// <summary>Bound to nothing. A command with no element is simply unavailable.</summary>
        None = -1,

        LeftStickX = 0,
        LeftStickY = 1,
        RightStickX = 2,
        RightStickY = 3,
        LeftTrigger = 4,
        RightTrigger = 5,
        A = 6,
        B = 7,
        X = 8,
        Y = 9,
        LeftShoulder = 10,
        RightShoulder = 11,
        Back = 12,
        Start = 13,
        LeftStickButton = 14,
        RightStickButton = 15,
        DPadUp = 16,
        DPadRight = 17,
        DPadDown = 18,
        DPadLeft = 19,
    }

    /// <summary>Everything the controller layer can be asked to do.</summary>
    internal enum ModCommand
    {
        None,
        NextTarget,
        PreviousTarget,
        NextCategory,
        PreviousCategory,
        ToggleGuidance,
        AnnounceGuidance,
        Autowalk,
        Rescan,
        EnemyRoster,
        CycleBeacon,
        CycleBeaconBack,
        WhereAmI,
        RepeatLast,
        Silence,
        Help,
        SettingsMenu,
        ReadPanel,
        NearestValidCell,
        MarkLog,
    }
}
