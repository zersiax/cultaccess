using System.Collections.Generic;
using CultAccess.Audio;
using UnityEngine;

namespace CultAccess.Combat
{
    /// <summary>
    /// Reduces all live hostile projectiles to the single most imminent predicted collision.
    /// Volleys therefore produce one actionable warning instead of one sound per particle.
    /// </summary>
    internal static class ProjectileThreatMonitor
    {
        // Projectile.Awake uses this exact fallback when a prefab has no circle collider.
        private const float GameProjectileFallbackRadius = 0.5f;

        // Earcon scheduling values are intentionally centralized for tuning from play logs.
        //
        // The repeat interval is the whole rate limit, deliberately. An earlier version let a
        // change of source bypass it and re-fire after a 0.08s floor, on the reasoning that a
        // newly closest threat deserves an immediate cue. In a room with eight arrows live the
        // identity of "most imminent" churns every few frames, so that exception governed
        // instead of the rule: in one miniboss room the source changed on 15 of 20 consecutive
        // cues and the result was heard as bullet hell. One cue per interval, panned to
        // whichever threat is most imminent at the time, is the contract.
        private const float FastestRepeatSeconds = 0.14f;
        private const float SlowestRepeatSeconds = 0.48f;

        private static readonly Dictionary<int, float> ProjectileRadii =
            new Dictionary<int, float>();
        private static readonly HashSet<string> LoggedMissingProjectileRadius =
            new HashSet<string>();

        private static float _nextCueAt;
        private static float _lastWarningAt = float.NegativeInfinity;
        private static int _lastWarningSourceId;
        private static string _lastWarningKind = "none";

        internal static float WarningHorizonSeconds = 1f;
        internal static bool Enabled = true;

        internal static void RegisterProjectileRadius(Projectile projectile, float radius)
        {
            if (projectile == null || radius <= 0f) return;
            ProjectileRadii[projectile.GetInstanceID()] = radius;
        }

        internal static void RegisterGrenade(
            GrenadeBullet grenade,
            Health.Team team,
            Vector3 target,
            float timeToTravel)
        {
            GrenadeThreatTracker.Register(grenade, team, target, timeToTravel);
        }

        internal static void GrenadeImpact(GrenadeBullet grenade, Vector3 target)
        {
            GrenadeThreatTracker.Impact(grenade, target);
        }

        internal static void UnregisterGrenade(GrenadeBullet grenade)
        {
            GrenadeThreatTracker.Unregister(grenade);
        }

        internal static void Tick(PlayerFarming player)
        {
            if (!Enabled || player == null || player.state == null ||
                player.state.CURRENT_STATE == StateMachine.State.Dodging ||
                PlayerRelic.TimeFrozen)
                return;

            var playerCollider = player.circleCollider2D;
            var playerRadius = ColliderGeometry.Radius(playerCollider);
            if (playerRadius <= 0f) return;

            var timeScale = Mathf.Max(Time.timeScale, Mathf.Epsilon);
            var playerVelocity = PlayerVelocity(player) * Time.timeScale;
            var best = default(CombatThreatCandidate);

            FindProjectileThreat(player, playerRadius, playerVelocity, ref best);
            GrenadeThreatTracker.FindSoonest(
                player, playerRadius, playerVelocity, timeScale,
                WarningHorizonSeconds, ref best);

            if (!best.Valid)
            {
                _nextCueAt = 0f;
                return;
            }

            var now = Time.unscaledTime;
            if (now < _nextCueAt) return;

            // Kept for the damage diagnostic's warningSourceMatch field, not for pacing.
            // Unity pools projectiles and reuses instance IDs, so this is not a stable
            // identity across a projectile's lifetime; nothing audible may depend on it.
            var sourceId = SourceId(best.Source);

            var urgency = 1f - Mathf.Clamp01(best.TimeToImpact / WarningHorizonSeconds);
            if (!CuePlayer.Play(best.CueKind, best.CuePosition, Mathf.Lerp(0.7f, 1f, urgency)))
                return;

            _lastWarningAt = now;
            _lastWarningSourceId = sourceId;
            _lastWarningKind = best.Kind;
            _nextCueAt = now + Mathf.Lerp(
                SlowestRepeatSeconds, FastestRepeatSeconds, urgency);

            CombatDiagnostics.Info(
                $"[combat threat] kind={best.Kind} source={Describe(best.Source)} " +
                $"id={sourceId} tti={best.TimeToImpact:0.000} " +
                $"closest={best.ClosestDistance:0.00} radius={best.CombinedRadius:0.00} " +
                $"currentDistance={best.CurrentDistance:0.00}");
        }

        /// <summary>
        /// Record a warning raised elsewhere, so damage diagnostics can tell a cued hit from
        /// an uncued one. Without this, every melee wind-up cue was invisible to
        /// <see cref="WarningContext"/> and every damage line reported
        /// <c>warningSourceMatch=False</c> — which is the exact field used to identify attack
        /// families that still need an adapter.
        ///
        /// Deliberately does not touch the projectile cue pacing: this reports what was
        /// warned, it does not schedule anything.
        /// </summary>
        internal static void RegisterWarning(GameObject source, string kind)
        {
            _lastWarningAt = Time.unscaledTime;
            _lastWarningSourceId = SourceId(source);
            _lastWarningKind = kind;
        }

        internal static string WarningContext(GameObject attacker)
        {
            var age = Time.unscaledTime - _lastWarningAt;
            var attackerId = SourceId(attacker);
            var match = attackerId != 0 && attackerId == _lastWarningSourceId;
            return $"warningKind={_lastWarningKind} warningAge={age:0.000} " +
                   $"warningSourceMatch={match}";
        }

        internal static void Reset()
        {
            GrenadeThreatTracker.Reset();
            _nextCueAt = 0f;
            _lastWarningAt = float.NegativeInfinity;
            _lastWarningSourceId = 0;
            _lastWarningKind = "none";
        }

        private static void FindProjectileThreat(
            PlayerFarming player,
            float playerRadius,
            Vector2 playerVelocity,
            ref CombatThreatCandidate best)
        {
            var projectiles = Projectile.Projectiles;
            if (projectiles == null) return;

            for (var i = 0; i < projectiles.Count; i++)
            {
                var projectile = projectiles[i];
                if (!CanHitPlayer(projectile, player)) continue;

                var speed = projectile.Speed * projectile.SpeedMultiplier * Time.timeScale;
                var radians = projectile.Angle * Mathf.Deg2Rad;
                var velocity = new Vector2(
                    Mathf.Cos(radians) * speed,
                    Mathf.Sin(radians) * speed);

                var localRadius = GameProjectileFallbackRadius;
                if (!ProjectileRadii.TryGetValue(projectile.GetInstanceID(), out localRadius))
                {
                    localRadius = GameProjectileFallbackRadius;
                    var typeName = projectile.GetType().Name;
                    if (LoggedMissingProjectileRadius.Add(typeName))
                        Plugin.Log.LogWarning(
                            $"[combat threat] missing captured radius for {typeName}; " +
                            $"using game's {GameProjectileFallbackRadius:0.0} fallback");
                }

                // Projectile.FixedUpdate passes its captured radius directly to
                // Physics2D.OverlapCircleNonAlloc; transform scale is not part of that call.
                var projectileRadius = localRadius;
                var combinedRadius =
                    (projectile.CollideOnlyTarget || projectile.CollideOnlyTargets
                        ? projectileRadius * 2f
                        : playerRadius + projectileRadius) + Physics2D.defaultContactOffset;
                var position = (Vector2)projectile.transform.position;
                var playerPosition = (Vector2)player.transform.position;

                if (!ThreatPrediction.TryPredict(
                        playerPosition.x, playerPosition.y,
                        playerVelocity.x, playerVelocity.y,
                        position.x, position.y,
                        velocity.x, velocity.y,
                        combinedRadius, WarningHorizonSeconds,
                        out var timeToClosest, out var closestDistance))
                    continue;

                var candidate = new CombatThreatCandidate
                {
                    Valid = true,
                    Source = projectile.gameObject,
                    CuePosition = projectile.transform.position,
                    CueKind = CueId.ProjectileThreat,
                    Kind = "projectile",
                    TimeToImpact = timeToClosest,
                    ClosestDistance = closestDistance,
                    CombinedRadius = combinedRadius,
                    CurrentDistance = Vector2.Distance(playerPosition, position),
                };
                CombatThreatCandidate.ChooseSoonest(candidate, ref best);
            }
        }

        private static bool CanHitPlayer(Projectile projectile, PlayerFarming player)
        {
            if (projectile == null || projectile.IsProjectilesParent ||
                !projectile.gameObject.activeInHierarchy ||
                projectile.team == Health.Team.PlayerTeam || projectile.Damage <= 0f)
                return false;

            if (projectile.ArrowImage != null && !projectile.ArrowImage.gameObject.activeSelf)
                return false;

            if (projectile.CollideOnlyTarget)
            {
                var target = projectile.GetTarget();
                if (target != null && target != player.health) return false;
            }

            if (projectile.CollideOnlyTargets)
            {
                var playerListed = false;
                foreach (var target in projectile.TargetObjects)
                {
                    if (target == null) continue;
                    var health = target.GetComponent<Health>();
                    if (health == player.health)
                    {
                        playerListed = true;
                        break;
                    }
                }
                if (!playerListed) return false;
            }

            return true;
        }

        private static Vector2 PlayerVelocity(PlayerFarming player)
        {
            var unit = player.unitObject;
            return unit == null ? Vector2.zero : new Vector2(unit.vx, unit.vy);
        }

        private static int SourceId(GameObject source)
        {
            if (source == null) return 0;
            var projectile = source.GetComponentInParent<Projectile>();
            if (projectile != null) return projectile.GetInstanceID();
            var grenade = source.GetComponentInParent<GrenadeBullet>();
            if (grenade != null) return grenade.GetInstanceID();
            return source.GetInstanceID();
        }

        private static string Describe(GameObject source)
        {
            if (source == null) return "none";
            var projectile = source.GetComponentInParent<Projectile>();
            if (projectile != null) return $"{projectile.GetType().Name}:{source.name}";
            var grenade = source.GetComponentInParent<GrenadeBullet>();
            if (grenade != null) return $"GrenadeBullet:{source.name}";
            return $"{source.GetType().Name}:{source.name}";
        }
    }
}
