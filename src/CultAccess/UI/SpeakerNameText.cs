using CultAccess.Util;

namespace CultAccess.UI
{
    /// <summary>
    /// Pure resolution of a conversation entry's <c>CharacterName</c> into a speakable
    /// name, for the case where the value did not resolve as a localisation term.
    ///
    /// The field carries two unrelated shapes and the game does not distinguish them. Most
    /// entries hold a term path such as <c>NAMES/CultLeaders/Dungeon2</c>, where only the
    /// last segment is a name. But nine sites assign a literal name wrapped in markup —
    /// <c>"&lt;color=yellow&gt;" + follower.Brain.Info.Name + "&lt;/color&gt;"</c> in
    /// <c>FollowerRecruit</c>, seven paths in <c>interaction_FollowerInteraction</c> and
    /// <c>Interaction_JellyFishBank</c> — and <c>PlayManualConvo</c> wraps one in sprite tags.
    ///
    /// Order is the entire difficulty. Splitting on the last slash before stripping markup
    /// finds the slash inside the closing <c>&lt;/color&gt;</c> and yields the literal string
    /// "color>", which is what live logs showed on every follower conversation in the game.
    /// </summary>
    public static class SpeakerNameText
    {
        /// <summary>
        /// Strip markup first, then take the last term-path segment. Returns null when
        /// nothing speakable survives.
        /// </summary>
        public static string FromRawCharacterName(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "-") return null;

            var name = RichText.Clean(raw);
            if (name.Length == 0) return null;

            var separator = name.LastIndexOf('/');
            if (separator >= 0) name = name.Substring(separator + 1);

            name = RichText.Humanise(name);
            return name.Length == 0 ? null : name;
        }
    }
}
