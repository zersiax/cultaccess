using System;
using System.Collections.Generic;
using System.Reflection;
using CultAccess.Navigation;
using HarmonyLib;

namespace CultAccess.Status
{
    /// <summary>
    /// Correlates the heart interaction with the resulting health-pool setters so an
    /// automatic collection is announced by item name as well as by its exact effect.
    /// </summary>
    [HarmonyPatch]
    internal static class HeartPickupPatches
    {
        private sealed class PickupState
        {
            public HealthPlayer Health;
            public PlayerFarming Player;
            public HealthSnapshot Before;
            public string Name;
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            var types = new[]
            {
                typeof(Interaction_RedHeart),
                typeof(Interaction_BlueHeart),
                typeof(Interaction_BlackHeart),
                typeof(Interaction_FireHeart),
                typeof(Interaction_IceHeart),
            };

            foreach (var type in types)
            {
                var method = AccessTools.Method(
                    type, nameof(Interaction.OnInteract), new[] { typeof(StateMachine) });
                if (method != null && method.DeclaringType == type)
                    yield return method;
                else
                    Plugin.Log.LogError($"Heart pickup hook missing OnInteract for {type.Name}");
            }
        }

        [HarmonyPrefix]
        private static void BeforePickup(
            Interaction_HeartPickupBase __instance,
            StateMachine __0,
            out PickupState __state)
        {
            __state = null;
            if (__instance == null || __0 == null) return;

            var player = __0.GetComponent<PlayerFarming>();
            var health = player?.health;
            if (player == null || health == null) return;

            __state = new PickupState
            {
                Health = health,
                Player = player,
                Before = PlayerStateAnnouncer.Capture(health),
                Name = HeartPickupTarget.Name(__instance),
            };
        }

        [HarmonyPostfix]
        private static void AfterPickup(PickupState __state)
        {
            if (__state == null) return;
            PlayerStateAnnouncer.HealthPickupCompleted(
                __state.Health, __state.Player, __state.Before, __state.Name);
        }
    }
}
