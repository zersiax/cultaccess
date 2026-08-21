using HarmonyLib;
using UnityEngine;

namespace CultAccess.Diagnostics
{
    /// <summary>
    /// Log-only causes which can precede an A* connectivity change. The A* completion hook
    /// in Navigator logs the resulting graph revision; pairing the two establishes whether
    /// a route opened after destruction, a room barrier change, or endpoint movement alone.
    /// </summary>
    internal static class NavigationGraphDiagnostics
    {
        private static int _causeSequence;
        private static string _lastCause;
        private static float _lastCauseAt;

        public static string LastCauseForLog
        {
            get
            {
                if (string.IsNullOrEmpty(_lastCause)) return "none";
                return $"{_lastCause};age={Time.unscaledTime - _lastCauseAt:0.00}s";
            }
        }

        public static void RecordCause(string kind, Component source, string detail)
        {
            if (!NavigationDiagnostics.Enabled) return;

            _causeSequence++;
            var type = source == null ? "none" : source.GetType().Name;
            var name = source == null ? "none" : Safe(source.gameObject.name);
            var position = source == null ? Vector3.zero : source.transform.position;
            _lastCause =
                $"sequence={_causeSequence},kind={Safe(kind)},type={type}," +
                $"object='{name}',world={Vector(position)},detail={Safe(detail)}";
            _lastCauseAt = Time.unscaledTime;
            Plugin.Log.LogInfo($"[nav graph cause] {_lastCause}");
        }

        public static void LogGraphLifecycle(string graphEvent, int revision, AstarPath graph)
        {
            if (!NavigationDiagnostics.Enabled) return;
            Plugin.Log.LogInfo(
                $"[nav graph] event={Safe(graphEvent)} revision={revision} " +
                $"graphInstance={(graph == null ? 0 : graph.GetInstanceID())} " +
                $"lastCause=\"{Safe(LastCauseForLog)}\"");
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\r", " ").Replace("\n", " ").Replace("\"", "'");
        }

        private static string Vector(Vector3 value) =>
            $"({value.x:0.00},{value.y:0.00},{value.z:0.00})";
    }

    [HarmonyPatch(typeof(BreakableTile), nameof(BreakableTile.OnDie))]
    internal static class BreakableTileNavigationDiagnostics
    {
        [HarmonyPostfix]
        private static void After(BreakableTile __instance)
        {
            if (__instance == null) return;
            var collider = __instance.GetComponent<BoxCollider2D>();
            NavigationGraphDiagnostics.RecordCause(
                "destructible-broken", __instance,
                $"colliderEnabled={collider != null && collider.enabled}");
        }
    }

    [HarmonyPatch(typeof(BreakableSpiderNest), "OnDie")]
    internal static class SpiderNestNavigationDiagnostics
    {
        [HarmonyPostfix]
        private static void After(BreakableSpiderNest __instance)
        {
            if (__instance == null) return;
            var collider = __instance.GetComponent<Collider2D>();
            NavigationGraphDiagnostics.RecordCause(
                "spider-nest-destroyed", __instance,
                $"colliderEnabled={collider != null && collider.enabled}");
        }
    }

    [HarmonyPatch(typeof(RoomLockController), nameof(RoomLockController.DoorUp))]
    internal static class RoomBarrierClosedNavigationDiagnostics
    {
        [HarmonyPrefix]
        private static void Before(RoomLockController __instance, out bool __state)
        {
            __state = __instance != null && __instance.Open;
        }

        [HarmonyPostfix]
        private static void After(RoomLockController __instance, bool __state)
        {
            if (__instance == null || __state == __instance.Open) return;
            NavigationGraphDiagnostics.RecordCause(
                "room-barrier-close", __instance,
                $"openBefore={__state},openAfter={__instance.Open}," +
                $"forcedLocked={__instance.ForcedLocked}," +
                $"blockerActive={__instance.BlockingCollider != null && __instance.BlockingCollider.activeInHierarchy}");
        }
    }

    [HarmonyPatch(typeof(RoomLockController), nameof(RoomLockController.DoorDown))]
    internal static class RoomBarrierOpenedNavigationDiagnostics
    {
        [HarmonyPrefix]
        private static void Before(RoomLockController __instance, out bool __state)
        {
            __state = __instance != null && __instance.Open;
        }

        [HarmonyPostfix]
        private static void After(RoomLockController __instance, bool __state)
        {
            if (__instance == null || __state == __instance.Open) return;
            NavigationGraphDiagnostics.RecordCause(
                "room-barrier-open", __instance,
                $"openBefore={__state},openAfter={__instance.Open}," +
                $"forcedLocked={__instance.ForcedLocked}," +
                $"blockerActive={__instance.BlockingCollider != null && __instance.BlockingCollider.activeInHierarchy}");
        }
    }

    [HarmonyPatch(typeof(BlockingDoor), nameof(BlockingDoor.Open))]
    internal static class MovingBarrierOpenedNavigationDiagnostics
    {
        [HarmonyPrefix]
        private static void Before(BlockingDoor __instance, out bool __state)
        {
            __state = ColliderEnabled(__instance);
        }

        [HarmonyPostfix]
        private static void After(BlockingDoor __instance, bool __state)
        {
            Record(__instance, "moving-barrier-open", __state);
        }

        internal static void Record(BlockingDoor instance, string kind, bool enabledBefore)
        {
            if (instance == null) return;
            var enabledAfter = ColliderEnabled(instance);
            if (enabledBefore == enabledAfter) return;
            NavigationGraphDiagnostics.RecordCause(
                kind, instance,
                $"colliderEnabledBefore={enabledBefore},colliderEnabledAfter={enabledAfter}");
        }

        internal static bool ColliderEnabled(BlockingDoor instance)
        {
            if (instance == null) return false;
            var enabled = false;
            foreach (var collider in instance.GetComponentsInChildren<BoxCollider2D>(true))
            {
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                {
                    enabled = true;
                    break;
                }
            }
            return enabled;
        }
    }

    [HarmonyPatch(typeof(BlockingDoor), nameof(BlockingDoor.Close))]
    internal static class MovingBarrierClosedNavigationDiagnostics
    {
        [HarmonyPrefix]
        private static void Before(BlockingDoor __instance, out bool __state)
        {
            __state = MovingBarrierOpenedNavigationDiagnostics.ColliderEnabled(__instance);
        }

        [HarmonyPostfix]
        private static void After(BlockingDoor __instance, bool __state)
        {
            MovingBarrierOpenedNavigationDiagnostics.Record(
                __instance, "moving-barrier-close", __state);
        }
    }
}
