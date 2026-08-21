using CultAccess.Speech;

namespace CultAccess.Status
{
    /// <summary>
    /// Says the one thing about a Twitch Help or Hinder vote that the game does not.
    ///
    /// While a vote runs, <c>TwitchHelpHinder</c> sets <c>BaseGoopDoor.LockDoor</c> and raises
    /// the base door for about 35 seconds. <see cref="Navigation.BasePassage"/> reads that
    /// flag, so the mod reported the exit as unavailable while giving no reason — a player
    /// would find the way out shut with nothing to explain it, and waiting is the only correct
    /// response.
    ///
    /// **Everything else about the event is already spoken, and this used to say it twice.**
    /// The game posts its own notifications and the mod reads them: "Twitch Chat are deciding
    /// your fate" when the vote opens, and — because
    /// <c>WorldManipulatorManager.TriggerManipulation</c> calls <c>PlayTwitchNotification</c>
    /// unconditionally under <c>if (twitch)</c> — a notification naming the outcome when it
    /// lands. An earlier version of this class announced both anyway. A live log caught the
    /// result: four announcements of one event, back to back, "paashaas is no longer
    /// dissenting", "All Followers have been cured thanks to Twitch Chat", "Twitch chat chose:
    /// Cure all Followers", "paashaas has stopped dissenting". Rules section 8 asks for one
    /// announcement per real-world event, and this is what breaking it sounds like.
    ///
    /// So only the gate is announced here. If the game ever stops announcing the vote itself,
    /// this is where that would go back.
    /// </summary>
    internal static class HelpHinderAnnouncer
    {
        private static bool _wasActive;

        /// <summary>True while chat is voting, which is also while the base door is held up.</summary>
        internal static bool Voting
        {
            get
            {
                try { return TwitchHelpHinder.Active; }
                catch (System.Exception) { return false; }
            }
        }

        /// <summary>
        /// Polled rather than hooked because <c>TwitchHelpHinder</c> exposes no event and its
        /// flow lives in a coroutine, where a Harmony postfix would fire when the enumerator is
        /// created rather than when the vote starts.
        /// </summary>
        internal static void Tick()
        {
            var active = Voting;
            if (active == _wasActive) return;
            _wasActive = active;

            if (!active) return;
            if (!Plugin.SpeechEnabled.Value || !Plugin.AnnounceHelpHinder.Value) return;

            Plugin.Log.LogInfo("[help hinder] vote started; announcing the held gate only");
            Speaker.Say(
                "The base gate is held shut until the vote resolves.",
                SpeechPriority.Queued);
        }
    }
}
