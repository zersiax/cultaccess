using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace CultAccess.Status
{
    [HarmonyPatch]
    internal static class HealthPlayerValuePatches
    {
        private sealed class PatchState
        {
            public bool IsOuter;
            public HealthSnapshot Before;
        }

        private static readonly Dictionary<HealthPlayer, int> SetterDepth =
            new Dictionary<HealthPlayer, int>();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            var properties = new[]
            {
                nameof(HealthPlayer.HP),
                nameof(HealthPlayer.totalHP),
                nameof(HealthPlayer.BlueHearts),
                nameof(HealthPlayer.BlackHearts),
                nameof(HealthPlayer.FireHearts),
                nameof(HealthPlayer.IceHearts),
                nameof(HealthPlayer.TotalSpiritHearts),
                nameof(HealthPlayer.SpiritHearts),
            };

            foreach (var property in properties)
            {
                var setter = AccessTools.PropertySetter(typeof(HealthPlayer), property);
                if (setter != null)
                    yield return setter;
                else
                    Plugin.Log.LogError($"Health state hook missing property setter: {property}");
            }
        }

        [HarmonyPrefix]
        private static void BeforeSetter(HealthPlayer __instance, out PatchState __state)
        {
            SetterDepth.TryGetValue(__instance, out var depth);
            __state = new PatchState
            {
                IsOuter = depth == 0,
                Before = depth == 0 ? PlayerStateAnnouncer.Capture(__instance) : default,
            };
            SetterDepth[__instance] = depth + 1;
        }

        [HarmonyPostfix]
        private static void AfterSetter(HealthPlayer __instance, PatchState __state)
        {
            if (SetterDepth.TryGetValue(__instance, out var depth) && depth > 1)
                SetterDepth[__instance] = depth - 1;
            else
                SetterDepth.Remove(__instance);

            if (__state != null && __state.IsOuter)
                PlayerStateAnnouncer.HealthChanged(__instance, __state.Before);
        }
    }

    [HarmonyPatch]
    internal static class HealthPlayerLifecyclePatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthPlayer), nameof(HealthPlayer.OnEnable))]
        private static void AfterEnable(HealthPlayer __instance) =>
            PlayerStateAnnouncer.ForgetHealth(__instance);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthPlayer), "OnDisable")]
        private static void BeforeDisable(HealthPlayer __instance) =>
            PlayerStateAnnouncer.ForgetHealth(__instance);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthPlayer), nameof(HealthPlayer.InitHP))]
        private static void BeforeInit(HealthPlayer __instance) =>
            PlayerStateAnnouncer.BeginHealthInitialization(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HealthPlayer), nameof(HealthPlayer.InitHP))]
        private static void AfterInit(HealthPlayer __instance) =>
            PlayerStateAnnouncer.EndHealthInitialization(__instance);
    }

    [HarmonyPatch]
    internal static class FervourValuePatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FaithAmmo), "set_Ammo")]
        private static void BeforeAmmo(FaithAmmo __instance, out float __state) =>
            __state = __instance?.Ammo ?? 0f;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(FaithAmmo), "set_Ammo")]
        private static void AfterAmmo(FaithAmmo __instance, float __state) =>
            PlayerStateAnnouncer.FervourChanged(__instance, __state);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(FaithAmmo), nameof(FaithAmmo.Init))]
        private static void AfterInit(FaithAmmo __instance) =>
            PlayerStateAnnouncer.ForgetFervour(__instance);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FaithAmmo), "OnDestroy")]
        private static void BeforeDestroy(FaithAmmo __instance) =>
            PlayerStateAnnouncer.ForgetFervour(__instance);
    }
}
