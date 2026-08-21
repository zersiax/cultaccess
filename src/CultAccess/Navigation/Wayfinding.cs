namespace CultAccess.Navigation
{
    /// <summary>Which channels walking guidance uses while it is running.</summary>
    public enum WayfindingMode
    {
        /// <summary>Spoken instructions and the audio beacon together.</summary>
        BeaconAndSpeech,

        /// <summary>The beacon alone. Nothing is spoken unless you ask for it.</summary>
        BeaconOnly,

        /// <summary>Spoken instructions alone. No beacon while guiding.</summary>
        SpeechOnly,
    }

    /// <summary>
    /// How the player wants to be guided.
    ///
    /// The two channels suit different players and different moments, and neither is a
    /// reduced version of the other. The beacon is continuous, pre-attentive and costs no
    /// listening effort, which is what you want while fighting. Spoken instructions name the
    /// destination and the distance, which is what you want in an unfamiliar base. Running
    /// both is right for many people and is too much for others, and until now it was the
    /// only option.
    ///
    /// What the mode governs is deliberately narrow: the *automatic* channels. Refusals,
    /// arrivals, and anything the player explicitly asked for are always spoken, in every
    /// mode. Silence in response to a keypress is indistinguishable from a crash, and a
    /// preference about ambient chatter is not consent to be told nothing.
    /// </summary>
    public static class Wayfinding
    {
        public static WayfindingMode Mode = WayfindingMode.BeaconAndSpeech;

        /// <summary>Whether the beacon should follow the route while guiding.</summary>
        public static bool UsesBeacon => Mode != WayfindingMode.SpeechOnly;

        /// <summary>
        /// Whether guidance speaks on its own — the periodic repeat and the interrupt when
        /// the heading changes. On-demand direction requests ignore this.
        /// </summary>
        public static bool SpeaksAutomatically => Mode != WayfindingMode.BeaconOnly;
    }
}
