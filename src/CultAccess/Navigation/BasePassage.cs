using CultAccess.Speech;
using HarmonyLib;
using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Adapts the cult's goop walls into traversal targets. <see cref="BaseGoopDoor"/>
    /// inherits <see cref="Interaction"/>, but deliberately disables every interaction
    /// action: the player crosses it by walking after its physical blocker is lowered.
    /// </summary>
    internal static class BasePassage
    {
        // BaseGoopDoor.SetDoorUp moves a player two world units to the inner side of
        // this exact blocker. Reusing that game-owned clearance puts guidance on the
        // far side of the wall instead of stopping at its centre and turning around.
        private const float GoopDoorPlayerClearance = 2f;

        public static bool ShouldInclude(BaseGoopDoor door)
        {
            if (door == null) return false;

            // The DLC entrance component exists before its bridge is revealed. Listing it
            // leaks a hidden destination and produced a second indistinguishable goop door.
            if (door.IsDLCDoor && DataManager.Instance != null &&
                !DataManager.Instance.OnboardedDLCEntrance)
                return false;

            return true;
        }

        public static string Name(BaseGoopDoor door)
        {
            var name = BareName(door);

            // A Help or Hinder vote raises the gate and holds it for about 35 seconds. Without
            // this the target simply reads as unavailable, which is worse than silence: the way
            // out is shut, nothing explains it, and waiting is the only correct response.
            return Status.HelpHinderAnnouncer.Voting ? name + ", held shut by a Twitch vote" : name;
        }

        private static string BareName(BaseGoopDoor door)
        {
            if (door == null) return "base passage";
            if (door.IsMainDoor) return "Old Faith passage";
            if (door.IsDLCDoor) return "Lamb Town passage";
            if (door.IsWoolhavenDoor) return "Woolhaven passage";
            return "base passage";
        }

        /// <summary>The same physical state that BaseGoopDoor.DoorDown writes.</summary>
        public static bool IsOpen(BaseGoopDoor door)
        {
            if (door == null || !door.gameObject.activeInHierarchy || !door.enabled)
                return false;

            return door.BlockingCollider != null
                ? !door.BlockingCollider.activeSelf
                : BaseGoopDoor.IsOpen && !BaseGoopDoor.LockDoor;
        }

        public static Vector3 Aim(
            BaseGoopDoor door, Vector3 origin, out bool returningToBase)
        {
            var anchor = door.BlockingCollider != null
                ? door.BlockingCollider.transform.position
                : door.transform.position;

            // SetDoorUp itself establishes the outward axes: DLC players are pushed left
            // of its wall, while main/Woolhaven players are pushed below theirs.
            var outward = door.IsDLCDoor ? Vector3.right : Vector3.up;
            returningToBase = Vector3.Dot(origin - anchor, outward) > 0f;
            var direction = returningToBase ? -outward : outward;
            var aim = anchor + direction * GoopDoorPlayerClearance;

            // The main passage has a dedicated automatic trigger that swaps the visible
            // base region. If it lies farther through the gate, guide into that trigger as
            // well. Its live bounds centre is authoritative and avoids a guessed distance.
            if (door.IsMainDoor && !returningToBase)
                aim = AimThroughNorthRegion(anchor, direction, aim);

            return aim;
        }

        private static Vector3 AimThroughNorthRegion(
            Vector3 anchor, Vector3 direction, Vector3 fallback)
        {
            BaseRegionEntrance nearest = null;
            var nearestDistance = float.MaxValue;
            foreach (var entrance in Object.FindObjectsOfType<BaseRegionEntrance>())
            {
                if (entrance == null || !entrance.gameObject.activeInHierarchy) continue;
                var delta = entrance.transform.position - anchor;
                if (Vector3.Dot(delta, direction) <= 0f) continue;

                var distance = delta.sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = entrance;
                nearestDistance = distance;
            }

            if (nearest == null) return fallback;

            var candidate = nearest.transform.position;
            var trigger = nearest.GetComponent<Collider2D>() ??
                          nearest.GetComponentInChildren<Collider2D>();
            if (trigger != null)
                candidate = trigger.bounds.center;

            return Vector3.Dot(candidate - anchor, direction) >
                   Vector3.Dot(fallback - anchor, direction)
                ? candidate
                : fallback;
        }

        public static void OnRegionCrossed(bool towardNorth)
        {
            if (Navigator.TrackedTarget?.BasePassageGate != null)
                Navigator.StopTracking(announce: false);

            Navigator.RefreshAfterWorldChange();
            Plugin.Log.LogInfo(
                $"[base passage] crossed direction={(towardNorth ? "north" : "south")}");
            Speaker.Say(towardNorth
                ? "Entered the Old Faith approach. The travel list is refreshed; continue north to the dungeon doors."
                : "Returned to the cult grounds.",
                SpeechPriority.Now);
        }
    }

    /// <summary>Mirrors the otherwise visual camera/region handoff after the north wall.</summary>
    [HarmonyPatch(typeof(BaseRegionEntrance), "DoTrigger")]
    internal static class BaseRegionEntrancePatch
    {
        [HarmonyPrefix]
        private static void BeforeTrigger(bool ___showingNorth, ref bool __state)
        {
            __state = ___showingNorth;
        }

        [HarmonyPostfix]
        private static void AfterTrigger(bool __state)
        {
            BasePassage.OnRegionCrossed(towardNorth: __state);
        }
    }
}
