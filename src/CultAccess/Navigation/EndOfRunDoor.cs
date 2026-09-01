using HarmonyLib;
using MMRoomGeneration;
using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>
    /// What the forward door of a miniboss room actually does, which is usually nothing.
    ///
    /// The final node of an adventure map is typed `MiniBossFloor` — `UIAdventureMapOverlayController`
    /// assigns that to the map's boss node directly — so that room is the end of the run and
    /// there is nothing further on the map to walk to. `EndOfDungeonContinue.Awake` decides what
    /// stands where a forward exit would be: with the dungeon already completed it activates a
    /// **teleporter** and hides the chest, and otherwise it activates the **chest** and leaves
    /// the teleporter switched off. The door objects themselves are only removed once
    /// `DungeonEndlessLevel` reaches its maximum of three.
    ///
    /// So on an ordinary run the door is physically present and completely inert. Nothing we
    /// otherwise look at says so: it is not `ConnectionTypes.False`, it has no
    /// `RoomLockController` holding it shut, and the state that decides is the *sibling*
    /// teleporter object being inactive. This is the shape principle 3 keeps warning about — we
    /// would have read the door's own label and lock state and confidently offered a route to
    /// somewhere the player cannot go.
    ///
    /// The active case is worth naming too, because it is not what "forward" implies: taking it
    /// rolls an entirely new map rather than continuing this one. `Door`'s `NextLayer` branch
    /// increments `GameManager.DungeonEndlessLevel` and runs `NewMapRoutine`.
    ///
    /// Written from the decompiled source. **No miniboss room has appeared in any session
    /// analysed so far**, so none of this has been confirmed against a running game.
    /// </summary>
    internal static class EndOfRunDoor
    {
        private static readonly AccessTools.FieldRef<EndOfDungeonContinue, GameObject> TeleporterField =
            BuildTeleporterAccessor();

        private static EndOfDungeonContinue _cached;
        private static bool _searched;

        /// <summary>
        /// Drop the cached lookup. Called when the A* graph is replaced, which is the game
        /// changing rooms — the same signal the target catalogue uses to notice it has gone
        /// stale, and for the same reason.
        /// </summary>
        internal static void Forget()
        {
            _cached = null;
            _searched = false;
        }

        /// <summary>
        /// A phrase describing this door's real state, or null when it is an ordinary door and
        /// nothing needs saying.
        /// </summary>
        internal static string Describe(Door door)
        {
            if (door == null || door.ConnectionType != GenerateRoom.ConnectionTypes.NextLayer)
                return null;

            var controller = Resolve();
            if (controller == null) return null;

            return CanContinue(controller)
                ? "opens a new adventure map rather than continuing this one"
                : "sealed; this is the last room of the run";
        }

        /// <summary>Whether a forward door here would do anything at all.</summary>
        internal static bool IsInert(Door door)
        {
            if (door == null || door.ConnectionType != GenerateRoom.ConnectionTypes.NextLayer)
                return false;

            var controller = Resolve();
            return controller != null && !CanContinue(controller);
        }

        private static bool CanContinue(EndOfDungeonContinue controller)
        {
            if (TeleporterField == null) return true;

            try
            {
                // The teleporter is the thing that actually moves the player on. Its own
                // activeSelf is the authority; the door around it is scenery either way.
                var teleporter = TeleporterField(controller);
                return teleporter != null && teleporter.activeInHierarchy;
            }
            catch (System.Exception e)
            {
                // Never silently: a wrong answer here routes the player to a dead door.
                Plugin.Log.LogWarning(
                    $"[end of run] could not read the continue teleporter, assuming it works: {e.Message}");
                return true;
            }
        }

        /// <summary>
        /// One scene search per room, cached including the miss.
        ///
        /// `EndOfDungeonContinue` has no registry to read, so this is the case principle 5
        /// allows a `FindObjectOfType` for — on demand, not per frame. Caching the negative
        /// matters as much as the positive: almost every room has no such controller, and an
        /// uncached miss would pay for a whole-hierarchy walk in all of them.
        /// </summary>
        private static EndOfDungeonContinue Resolve()
        {
            if (_cached != null) return _cached;
            if (_searched) return null;

            _searched = true;
            try
            {
                _cached = Object.FindObjectOfType<EndOfDungeonContinue>();
                if (_cached != null)
                    Plugin.Log.LogInfo(
                        $"[end of run] room has an end-of-dungeon door; continue available={CanContinue(_cached)}");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[end of run] lookup failed: {e.Message}");
            }

            return _cached;
        }

        private static AccessTools.FieldRef<EndOfDungeonContinue, GameObject> BuildTeleporterAccessor()
        {
            try
            {
                var field = AccessTools.Field(typeof(EndOfDungeonContinue), "teleporter");
                if (field != null)
                    return AccessTools.FieldRefAccess<EndOfDungeonContinue, GameObject>(field);

                Plugin.Log.LogWarning(
                    "[end of run] EndOfDungeonContinue has no 'teleporter' field; forward doors " +
                    "in a miniboss room will be described as ordinary exits.");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[end of run] could not bind the teleporter field: {e.Message}");
            }

            return null;
        }
    }
}
