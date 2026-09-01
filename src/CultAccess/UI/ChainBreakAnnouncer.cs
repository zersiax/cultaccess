using CultAccess.Speech;

namespace CultAccess.UI
{
    /// <summary>
    /// Says that a chain has broken on the dungeon door, and how many remain.
    ///
    /// `BaseChainDoor` guards the four dungeon entrances with five chains. Walking within
    /// eight units of it after beating a bishop runs `PlayRoutine`, which increments
    /// `DataManager.DoorRoomChainProgress`, plays `event:/door/chain_break_sequence` and a
    /// `break{n}` animation, and holds the camera for twelve seconds via
    /// `OnConversationNext(gameObject, 12f)`.
    ///
    /// That hold has no dialogue behind it. So the whole thing — twelve seconds, a sound, an
    /// animation, and one of five steps toward opening the rest of the game — reached the
    /// player as silence, and was only noticed because a tester mentioned chains and a session
    /// log turned out to contain `SPeaker game object addedCHAIN DOOR`.
    ///
    /// Note this fires on approaching the door, not on the kill. A player who beats a bishop
    /// and goes straight back out will meet it much later, which is another reason it needs
    /// saying: there is nothing nearby to connect it to.
    /// </summary>
    internal static class ChainBreakAnnouncer
    {
        /// <summary>The door carries five chains; the fifth is the one that opens it.</summary>
        private const int FinalChain = 5;

        internal static bool Enabled = true;

        private static int _lastAnnounced = -1;

        /// <summary>
        /// Driven from the plugin's update, watching the counter rather than patching
        /// `PlayRoutine`.
        ///
        /// The routine is a coroutine that increments the counter partway through a
        /// twelve-second sequence, so a prefix would fire too early and a postfix would fire
        /// when the iterator was created rather than when the chain broke. Watching the value
        /// catches the actual moment, needs no patch to keep working across a game update, and
        /// costs a static field read per frame.
        /// </summary>
        internal static void Tick()
        {
            if (!Enabled) return;

            int progress;
            try
            {
                var data = DataManager.Instance;
                if (data == null) return;

                progress = data.DoorRoomChainProgress;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[chain door] could not read the chain progress: {e.Message}");
                return;
            }

            if (progress == _lastAnnounced) return;

            var previous = _lastAnnounced;
            _lastAnnounced = progress;

            // Only a forward step counts. Loading a save sets this from -1 to whatever the
            // player had already reached, and announcing four chains at the title screen would
            // be worse than saying nothing.
            if (previous < 0 || progress <= previous || progress <= 0) return;

            Plugin.Log.LogInfo(
                $"[chain door] chain broken progress={progress} of {FinalChain}");

            Speaker.Say(
                progress >= FinalChain
                    ? "The last chain breaks. The dungeon door is open."
                    : $"A chain breaks on the dungeon door. {progress} of {FinalChain}.",
                SpeechPriority.Queued);
        }

        internal static void Reset() => _lastAnnounced = -1;
    }
}
