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

    /// <summary>
    /// The Forest Flying Worm. Same shape as the scuttle-swiper and the same evidence:
    /// <c>EnemyBat.AttackRoutine</c> sets <c>SignPostAttackAnimation</c>, spends a wind-up,
    /// then switches to <c>AttackAnimation</c> and enables <c>damageColliderEvents</c>.
    ///
    /// The wind-up is a literal <c>Duration = 1f</c> in the routine — a full second, the
    /// longest telegraph of any enemy adapted so far — advanced by <c>Time.deltaTime *
    /// Spine.timeScale</c>, so hitstop stretches it and the cue only ever lands early.
    ///
    /// No instance-type filter, unlike the scuttle-swiper. <c>AttackRoutine</c> is virtual,
    /// and Harmony patches this declared body: an override that replaces it never reaches
    /// here, and one that calls <c>base</c> runs this exact timing. The cue is therefore
    /// honest for every subclass by construction rather than by a list needing maintenance.
    /// </summary>
    [HarmonyPatch(typeof(EnemyBat), "AttackRoutine")]
    internal static class BatMeleeWarningPatch
    {
        private const float WindupSeconds = 1f;

        [HarmonyPrefix]
        private static void BeforeWindup(EnemyBat __instance)
        {
            if (__instance == null || __instance.health == null ||
                __instance.health.team == Health.Team.PlayerTeam)
                return;

            var target = __instance.GetClosestTarget();
            if (target == null || !target.isPlayer) return;

            CombatAssist.WarnMelee(__instance.gameObject, WindupSeconds, "flying-worm");
        }
    }

    /// <summary>
    /// The Forest Miniboss Diving Maggot, which took two hits with no warning at all in the
    /// last session — <c>warningAge=87.0</c>, meaning nothing had fired for a minute and a half.
    ///
    /// Its dives all live inside one <c>DiveMoveRoutine</c> coroutine, so a prefix there would
    /// fire once for a run of three or four leaps. <c>GetNewTargetPosition</c> is the per-dive
    /// hook: public, called from exactly one place — the top of each loop iteration, right
    /// before the jump — and its return value is the game's own "this dive is happening".
    ///
    /// The lead time is the game's own expression, <c>distance / MoveSpeed</c>, read live so
    /// the 1.35x <c>MoveSpeed</c> boost the miniboss gives itself in its second phase is
    /// picked up without anyone having to notice it exists. It is a slight underestimate: the
    /// routine can sit in a hitstop wait between here and the jump, which only makes the cue
    /// early.
    ///
    /// The warning is aimed at the landing point rather than at the maggot. This attack is
    /// the reason that option exists — it crosses the room mid-cue, and the damage collider
    /// is enabled for 0.3 s where it lands, not along the arc.
    /// </summary>
    [HarmonyPatch(
        typeof(EnemyMaggotMiniBoss), nameof(EnemyMaggotMiniBoss.GetNewTargetPosition))]
    internal static class MaggotMiniBossDiveWarningPatch
    {
        [HarmonyPostfix]
        private static void AfterTargetChosen(
            EnemyMaggotMiniBoss __instance, bool __result, Vector3 ___TargetPosition)
        {
            if (!__result || __instance == null || __instance.health == null ||
                __instance.health.team == Health.Team.PlayerTeam)
                return;

            var speed = __instance.MoveSpeed;
            if (speed <= 0f) return;

            // Mirrors DiveMoveRoutine exactly, including using the full 3D distance the game
            // uses to derive the same Duration. The landing point handed to the cue is
            // flattened, because that is where the damage lands.
            var travel = Vector3.Distance(__instance.transform.position, ___TargetPosition);
            var landing = ___TargetPosition;
            landing.z = 0f;

            CombatAssist.WarnMelee(
                __instance.gameObject, travel / speed, "diving-maggot", landing);
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
