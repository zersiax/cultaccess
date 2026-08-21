using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CultAccess.Combat
{
    [HarmonyPatch(typeof(PlayerFarming), nameof(PlayerFarming.DodgeRoll))]
    internal static class DodgeCuePatch
    {
        [HarmonyPostfix]
        private static void AfterDodge(PlayerFarming __instance, bool __result)
        {
            if (__result) CombatAssist.OnDodge(__instance);
        }
    }

    /// <summary>
    /// The early Darkwood scamp commits to an attack by starting this iterator, then spends
    /// SignPostAttackDuration in its visual/audio wind-up before enabling its damage collider.
    /// Patch the base implementation only: charger and scout overrides are different attacks
    /// and need their own timing evidence rather than inheriting this cue dishonestly.
    /// </summary>
    [HarmonyPatch(typeof(EnemyScuttleSwiper), "AttackRoutine")]
    internal static class ScuttleSwiperMeleeWarningPatch
    {
        private static readonly FieldInfo TargetObjectField =
            AccessTools.Field(typeof(EnemyScuttleSwiper), "TargetObject");

        [HarmonyPrefix]
        private static void BeforeWindup(EnemyScuttleSwiper __instance)
        {
            if (__instance == null || __instance.health == null ||
                __instance.health.team == Health.Team.PlayerTeam)
                return;

            var player = PlayerFarming.Instance;
            var target = TargetObjectField?.GetValue(__instance) as GameObject;
            if (player == null || target == null ||
                target.GetComponentInParent<PlayerFarming>() != player)
                return;

            CombatAssist.WarnMelee(
                __instance.gameObject,
                __instance.SignPostAttackDuration,
                "scuttle-swiper");
        }
    }

    [HarmonyPatch(typeof(Projectile), "Awake")]
    internal static class ProjectileRadiusPatch
    {
        [HarmonyPostfix]
        private static void AfterAwake(Projectile __instance, float ___radius) =>
            ProjectileThreatMonitor.RegisterProjectileRadius(__instance, ___radius);
    }

    /// <summary>
    /// Projectile collision methods reject a dodging player before calling Health.DealDamage,
    /// so this exact collision boundary is needed for a truthful evade confirmation. Patch all
    /// declared overrides as well as the base implementation.
    /// </summary>
    [HarmonyPatch]
    internal static class ProjectileEvadePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var parameterTypes = new[] { typeof(Collider2D) };
            foreach (var type in typeof(Projectile).Assembly.GetTypes())
            {
                if (!typeof(Projectile).IsAssignableFrom(type)) continue;

                var method = type.GetMethod(
                    "OnRayEnter2D",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly,
                    binder: null,
                    types: parameterTypes,
                    modifiers: null);
                if (method != null) yield return method;
            }
        }

        [HarmonyPrefix]
        private static void BeforeCollision(Projectile __instance, Collider2D collider)
        {
            if (!CombatAssist.Enabled || __instance == null || collider == null ||
                __instance.team == Health.Team.PlayerTeam)
                return;

            var health = collider.GetComponent<Health>();
            if (health == null || !health.isPlayer || health.state == null ||
                health.state.CURRENT_STATE != StateMachine.State.Dodging ||
                health.invincible || health.untouchable || health.IgnoreProjectiles)
                return;

            if (__instance.ArrowImage != null && !__instance.ArrowImage.gameObject.activeSelf)
                return;

            CombatAssist.ConfirmEvade(__instance.gameObject, "projectile-overlap");
        }
    }

    [HarmonyPatch(typeof(Health), nameof(Health.DealDamage))]
    internal static class PlayerDamageDiagnosticsPatch
    {
        private struct DamageState
        {
            public bool Track;
            public bool RejectedByDodge;
            public float Before;
        }

        [HarmonyPrefix]
        private static void BeforeDamage(
            Health __instance,
            bool dealDamageImmediately,
            out DamageState __state)
        {
            __state = default;
            if (!CombatAssist.Enabled || __instance == null || !__instance.isPlayer) return;

            __state.Track = true;
            __state.Before = __instance.CurrentHP;
            __state.RejectedByDodge =
                __instance.enabled && !__instance.invincible && !__instance.untouchable &&
                __instance.GodMode != Health.CheatMode.God && !dealDamageImmediately &&
                __instance.state != null &&
                __instance.state.CURRENT_STATE == StateMachine.State.Dodging;
        }

        [HarmonyPostfix]
        private static void AfterDamage(
            Health __instance,
            float Damage,
            GameObject Attacker,
            Vector3 AttackLocation,
            Health.AttackTypes AttackType,
            bool dealDamageImmediately,
            Health.AttackFlags AttackFlags,
            bool __result,
            DamageState __state)
        {
            if (!__state.Track) return;

            CombatAssist.RecordDamage(
                Attacker, AttackLocation, AttackType, AttackFlags, Damage,
                __state.Before, __instance.CurrentHP, dealDamageImmediately, __result,
                __state.RejectedByDodge && !__result);
        }
    }

    [HarmonyPatch(typeof(GrenadeBullet), nameof(GrenadeBullet.Play))]
    internal static class GrenadePlayPatch
    {
        [HarmonyPostfix]
        private static void AfterPlay(
            GrenadeBullet __instance,
            Health.Team team,
            float ___timeToTravel,
            Vector3 ___targetPosition)
        {
            if (CombatAssist.Enabled)
                ProjectileThreatMonitor.RegisterGrenade(
                    __instance, team, ___targetPosition, ___timeToTravel);
        }
    }

    [HarmonyPatch(typeof(GrenadeBullet), "DoCollision")]
    internal static class GrenadeImpactPatch
    {
        [HarmonyPrefix]
        private static void BeforeImpact(GrenadeBullet __instance, Vector3 ___targetPosition) =>
            ProjectileThreatMonitor.GrenadeImpact(__instance, ___targetPosition);
    }

    [HarmonyPatch(typeof(GrenadeBullet), "OnDisable")]
    internal static class GrenadeDisablePatch
    {
        [HarmonyPrefix]
        private static void BeforeDisable(GrenadeBullet __instance) =>
            ProjectileThreatMonitor.UnregisterGrenade(__instance);
    }
}
