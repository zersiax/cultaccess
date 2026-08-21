using CultAccess.Util;
using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Adapts enterable buildings into traversal targets.
    ///
    /// A large structure is not one interactable square. The player walks into a doorway and
    /// the thing they came for lives inside, in a different <c>FollowerLocation</c>. The
    /// outside of such a building exposes no <see cref="Interaction"/> at all — the Temple's
    /// own <c>Interaction_Temple</c> derives from <c>BaseMonoBehaviour</c> despite its name —
    /// so scanning <c>Interaction.interactions</c> can never find a way in. That left the
    /// "preach a Sermon in your Temple" objective with no reachable target.
    ///
    /// <c>EnterBuilding</c> is the way in: a trigger volume carrying a destination location,
    /// entered by walking, exactly like the cult's goop passages. This adapter is deliberately
    /// generic rather than Temple-specific, because the same component fronts the Blacksmith,
    /// the shops, Sozo's cave and the rest.
    /// </summary>
    internal static class BuildingEntrance
    {
        public static bool ShouldInclude(EnterBuilding entrance)
        {
            if (entrance == null || !entrance.gameObject.activeInHierarchy) return false;

            // OnTriggerEnter2D returns immediately unless a collider exists; without one this
            // doorway cannot be walked through however it looks.
            return Trigger(entrance) != null;
        }

        /// <summary>
        /// Prefer the owning structure's localized name, so the player hears the same word the
        /// build menu and the objective use. The destination enum is the fallback.
        /// </summary>
        public static string Name(EnterBuilding entrance)
        {
            if (entrance == null) return "building entrance";

            try
            {
                var structure = entrance.GetComponentInParent<Structure>();
                if (structure != null)
                {
                    var data = structure.Structure_Info;
                    var type = data == null ? structure.Type : data.Type;
                    var structureName = Building.PlacementDescriber.Name(type);
                    if (RichText.HasSemanticText(structureName)) return structureName;
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not read building entrance structure: {e.Message}");
            }

            return DestinationName(entrance.Destination);
        }

        /// <summary>
        /// Fallback for doorways with no owning structure — the Door Room transition and the
        /// return-to-base exits inside buildings. The raw enum is not player vocabulary:
        /// "Base" says nothing, and two exits both called "Base" are indistinguishable in a
        /// spoken list. Use the words the rest of the mod already uses out loud.
        /// </summary>
        internal static string DestinationName(FollowerLocation destination)
        {
            switch (destination)
            {
                case FollowerLocation.Base:
                    return "cult grounds";
                case FollowerLocation.Church:
                    return "Temple";
                case FollowerLocation.DoorRoom:
                    return "dungeon door room";
                default:
                    var humanised = RichText.Humanise(destination.ToString());
                    return humanised.Length == 0 ? "building entrance" : humanised;
            }
        }

        /// <summary>
        /// The serialized blocker is the game's own physical gate for these doorways. When a
        /// building has none, an active trigger is the only condition the game itself checks.
        /// </summary>
        public static bool IsOpen(EnterBuilding entrance)
        {
            if (!ShouldInclude(entrance) || !entrance.enabled) return false;

            var blocker = entrance.blocker;
            if (blocker != null && blocker.activeSelf) return false;

            var trigger = Trigger(entrance);
            return trigger != null && trigger.enabled;
        }

        /// <summary>
        /// Aim at the trigger's live bounds centre rather than the transform: the doorway
        /// collider is what the player has to physically cross, and on these prefabs it is
        /// routinely offset from the object's origin.
        /// </summary>
        public static Vector3 Aim(EnterBuilding entrance)
        {
            if (entrance == null) return Vector3.zero;

            var trigger = Trigger(entrance);
            return trigger != null ? trigger.bounds.center : entrance.transform.position;
        }

        public static string Describe(string name, EnterBuilding entrance) =>
            BuildingEntranceText.Describe(name, IsOpen(entrance));

        public static string Arrival(string name, EnterBuilding entrance) =>
            BuildingEntranceText.Arrival(name, IsOpen(entrance));

        private static Collider2D Trigger(EnterBuilding entrance)
        {
            if (entrance == null) return null;
            return entrance.GetComponent<Collider2D>() ??
                   entrance.GetComponentInChildren<Collider2D>();
        }
    }
}
