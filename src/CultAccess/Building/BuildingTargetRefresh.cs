using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CultAccess.Building
{
    /// <summary>
    /// Keeps the navigation catalogue in step with the two-stage building lifecycle:
    /// placement creates a build-site interaction immediately, while completion replaces it
    /// with an asynchronously loaded prefab and that prefab's own interactions.
    /// </summary>
    internal static class BuildingTargetRefresh
    {
        private const float SettleSeconds = 0.12f;
        private const float CompletionMatchLifetime = 120f;

        private sealed class ExpectedCompletion
        {
            public StructureBrain.TYPES Type;
            public FollowerLocation Location;
            public Vector2Int GridPosition;
            public float ExpiresAt;
        }

        private static readonly List<ExpectedCompletion> Expected =
            new List<ExpectedCompletion>();

        private static bool _refreshPending;
        private static int _refreshFrame;
        private static float _refreshAt;
        private static string _reason;

        internal static void Schedule(string reason)
        {
            _refreshPending = true;
            _refreshFrame = Time.frameCount + 2;
            _refreshAt = Time.unscaledTime + SettleSeconds;
            _reason = reason;
        }

        internal static void ExpectCompletion(StructuresData data)
        {
            if (data == null || data.ToBuildType == StructureBrain.TYPES.NONE) return;

            for (var i = Expected.Count - 1; i >= 0; i--)
            {
                var item = Expected[i];
                if (item.Type == data.ToBuildType && item.Location == data.Location &&
                    item.GridPosition == data.GridTilePosition)
                    Expected.RemoveAt(i);
            }

            Expected.Add(new ExpectedCompletion
            {
                Type = data.ToBuildType,
                Location = data.Location,
                GridPosition = data.GridTilePosition,
                ExpiresAt = Time.unscaledTime + CompletionMatchLifetime,
            });
            Plugin.Log.LogInfo(
                $"[building targets] awaiting finished prefab type={data.ToBuildType} " +
                $"location={data.Location} grid={data.GridTilePosition}");
        }

        internal static void StructureReady(StructuresData data, GameObject placedObject)
        {
            if (data == null || placedObject == null) return;

            for (var i = Expected.Count - 1; i >= 0; i--)
            {
                var item = Expected[i];
                if (item.Type != data.Type || item.Location != data.Location ||
                    item.GridPosition != data.GridTilePosition)
                    continue;

                Expected.RemoveAt(i);
                Schedule($"completed-{data.Type}");
                return;
            }
        }

        internal static void Tick()
        {
            for (var i = Expected.Count - 1; i >= 0; i--)
                if (Time.unscaledTime >= Expected[i].ExpiresAt)
                    Expected.RemoveAt(i);

            if (!_refreshPending || Time.frameCount < _refreshFrame ||
                Time.unscaledTime < _refreshAt)
                return;

            _refreshPending = false;
            Plugin.Log.LogInfo($"[building targets] rescan reason={_reason}");
            Navigation.Navigator.RefreshAfterWorldChange();
        }

        internal static void Shutdown()
        {
            Expected.Clear();
            _refreshPending = false;
            _reason = null;
        }
    }

    [HarmonyPatch(typeof(Structures_BuildSite), nameof(Structures_BuildSite.Build))]
    internal static class BuildSiteCompletionPatch
    {
        [HarmonyPrefix]
        private static void BeforeBuild(Structures_BuildSite __instance)
        {
            BuildingTargetRefresh.ExpectCompletion(__instance?.Data);
        }
    }

    [HarmonyPatch(typeof(Structures_BuildSiteProject), "Build")]
    internal static class BuildSiteProjectCompletionPatch
    {
        [HarmonyPrefix]
        private static void BeforeBuild(Structures_BuildSiteProject __instance)
        {
            BuildingTargetRefresh.ExpectCompletion(__instance?.Data);
        }
    }

    [HarmonyPatch(
        typeof(LocationManager),
        nameof(LocationManager.PlaceStructure),
        new System.Type[] { typeof(StructuresData), typeof(GameObject) })]
    internal static class FinishedStructurePlacementPatch
    {
        [HarmonyPostfix]
        private static void AfterPlacement(
            StructuresData structure, GameObject __result)
        {
            BuildingTargetRefresh.StructureReady(structure, __result);
        }
    }
}
