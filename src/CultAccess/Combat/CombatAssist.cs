using CultAccess.Audio;
using UnityEngine;

namespace CultAccess.Combat
{
    /// <summary>Coordinates real-time combat cues and their shared noise budget.</summary>
    internal static class CombatAssist
    {
        private static PlayerFarming _player;
        private static bool _evadeConfirmedThisDodge;
        private static bool _wasOperational;

        // Derived from the cue settings rather than held separately, so there is exactly one
        // switch per sound. A second flag that also had to be on was the obvious way to build
        // this and the wrong one: a player who turns a cue off in the menu and still hears it
        // has no way to work out which of two settings won. Deriving them here also stops the
        // detection work — raycasts, trajectory prediction — whenever nothing would sound.
        internal static bool Enabled =>
            CueSettings.MasterEnabled && CueSettings.GroupEnabled(CueGroup.Combat);

        internal static bool WallCues =>
            CueSettings.Enabled(CueId.WallNear) || CueSettings.Enabled(CueId.WallBlocked);

        internal static bool DodgeCues =>
            CueSettings.Enabled(CueId.DodgeDirection) || CueSettings.Enabled(CueId.DodgeBlocked);

        internal static bool MeleeWarnings => CueSettings.Enabled(CueId.MeleeThreat);

        internal static bool ProjectileWarnings =>
            CueSettings.Enabled(CueId.ProjectileThreat) || CueSettings.Enabled(CueId.AreaThreat);

        internal static bool EvadeConfirmation => CueSettings.Enabled(CueId.DodgeAvoidedHit);

        internal static void Tick()
        {
            var player = PlayerFarming.Instance;
            var operational = Enabled && player != null && player.gameObject.activeInHierarchy;
            if (!operational)
            {
                if (_wasOperational) ResetRuntime();
                _wasOperational = false;
                return;
            }

            if (_player != player)
            {
                ResetRuntime();
                _player = player;
            }

            _wasOperational = true;
            if (Time.timeScale <= 0f) return;

            CombatLifecycle.Tick(player);
            MeleeWarningSchedule.Tick();

            if (!PlayerCanReceiveRealtimeCues(player))
            {
                ObstacleSonar.Reset();
                return;
            }

            if (WallCues)
                ObstacleSonar.Tick(player);
            else
                ObstacleSonar.Reset();

            ProjectileThreatMonitor.Enabled = ProjectileWarnings;
            if (ProjectileWarnings)
                ProjectileThreatMonitor.Tick(player);
        }

        internal static void OnDodge(PlayerFarming player)
        {
            if (!Enabled || player == null) return;
            _evadeConfirmedThisDodge = false;
            if (DodgeCues) ObstacleSonar.OnDodge(player);
        }

        internal static void ConfirmEvade(GameObject source, string mechanism)
        {
            if (!Enabled || _evadeConfirmedThisDodge) return;

            var player = PlayerFarming.Instance;
            if (player == null) return;

            _evadeConfirmedThisDodge = true;
            if (EvadeConfirmation)
                CuePlayer.Play(CueId.DodgeAvoidedHit, player.transform.position);

            CombatDiagnostics.Info(
                $"[combat evade] mechanism={mechanism} source={DescribeSource(source)} " +
                ProjectileThreatMonitor.WarningContext(source));
        }

        /// <summary>
        /// An enemy has committed to a melee attack that will land in
        /// <paramref name="leadSeconds"/>. The cue is queued rather than played now: see
        /// <see cref="MeleeWarningSchedule"/> for why reacting the instant an attack starts
        /// is guaranteed to dodge too early.
        /// </summary>
        internal static void WarnMelee(GameObject source, float leadSeconds, string family)
        {
            var player = PlayerFarming.Instance;
            if (!Enabled || !MeleeWarnings || source == null || player == null ||
                Time.timeScale <= 0f || !PlayerCanReceiveRealtimeCues(player))
                return;

            MeleeWarningSchedule.Schedule(source, leadSeconds, family);
        }

        /// <summary>Play a queued melee warning, once its moment has arrived.</summary>
        internal static void SoundMeleeWarning(
            GameObject source, float remainingSeconds, string family)
        {
            var player = PlayerFarming.Instance;
            if (!Enabled || !MeleeWarnings || source == null || player == null ||
                Time.timeScale <= 0f || !PlayerCanReceiveRealtimeCues(player))
                return;

            var played = CuePlayer.Play(CueId.MeleeThreat, source.transform.position);
            if (played) ProjectileThreatMonitor.RegisterWarning(source, "melee");

            CombatDiagnostics.Info(
                $"[combat melee] family={family} source={DescribeSource(source)} " +
                $"impactIn={remainingSeconds:0.000} distance=" +
                $"{Vector3.Distance(player.transform.position, source.transform.position):0.00} " +
                $"cue={played}");
        }

        /// <summary>
        /// Warn about a static hazard that has just committed to hurting whoever is standing
        /// on it. Unlike a melee wind-up there is no attacker to face and nowhere to dodge
        /// to — the only useful action is to leave — so the cue is placed at the hazard
        /// itself and uses the loudest, most urgent sound in the vocabulary.
        /// </summary>
        internal static void WarnStaticHazard(GameObject source, float leadSeconds, string family)
        {
            var player = PlayerFarming.Instance;
            if (!Enabled || source == null || player == null || Time.timeScale <= 0f ||
                !PlayerCanReceiveRealtimeCues(player))
                return;

            var played = CuePlayer.Play(CueId.StaticTrap, source.transform.position);
            if (played) ProjectileThreatMonitor.RegisterWarning(source, "static-hazard");

            CombatDiagnostics.Info(
                $"[combat trap] family={family} source={DescribeSource(source)} " +
                $"lead={leadSeconds:0.000} distance=" +
                $"{Vector3.Distance(player.transform.position, source.transform.position):0.00} " +
                $"cue={played}");
        }

        internal static void RecordDamage(
            GameObject attacker,
            Vector3 attackLocation,
            Health.AttackTypes attackType,
            Health.AttackFlags attackFlags,
            float requestedDamage,
            float before,
            float after,
            bool immediate,
            bool result,
            bool rejectedByDodge)
        {
            var stage = rejectedByDodge
                ? "dodge-rejected"
                : immediate
                    ? "resolved"
                    : result ? "queued" : "queue-rejected";
            CombatDiagnostics.Info(
                $"[combat damage] stage={stage} source={DescribeSource(attacker)} " +
                $"attack={attackType} flags={attackFlags} requested={requestedDamage:0.###} " +
                $"health={before:0.###}->{after:0.###} result={result} " +
                $"location=({attackLocation.x:0.00},{attackLocation.y:0.00}) " +
                ProjectileThreatMonitor.WarningContext(attacker));

            if (rejectedByDodge) ConfirmEvade(attacker, "damage-call");
        }

        internal static void Shutdown()
        {
            ResetRuntime();
            CombatLifecycle.Shutdown();
            CuePlayer.Shutdown();
        }

        private static void ResetRuntime()
        {
            _player = null;
            _evadeConfirmedThisDodge = false;
            ObstacleSonar.Reset();
            ProjectileThreatMonitor.Reset();
            MeleeWarningSchedule.Clear();
            CombatLifecycle.Reset();
        }

        /// <summary>
        /// Whether real-time cues make sense right now. False during death, spawning and
        /// scripted animations, where the player has no control to exercise on the information.
        /// Shared with the always-on layer so both fall silent at the same moments.
        /// </summary>
        internal static bool PlayerCanReceiveRealtimeCues(PlayerFarming player)
        {
            if (player.GoToAndStopping || player.state == null) return false;

            switch (player.state.CURRENT_STATE)
            {
                case StateMachine.State.InActive:
                case StateMachine.State.CustomAction0:
                case StateMachine.State.CustomAnimation:
                case StateMachine.State.SpawnIn:
                case StateMachine.State.SpawnOut:
                case StateMachine.State.Dieing:
                case StateMachine.State.Dead:
                case StateMachine.State.GameOver:
                case StateMachine.State.FinalGameOver:
                    return false;
                default:
                    return true;
            }
        }

        private static string DescribeSource(GameObject source)
        {
            if (source == null) return "none";

            var projectile = source.GetComponentInParent<Projectile>();
            if (projectile != null)
                return $"{projectile.GetType().Name}:{source.name}";

            var grenade = source.GetComponentInParent<GrenadeBullet>();
            if (grenade != null)
                return $"GrenadeBullet:{source.name}";

            var unit = source.GetComponentInParent<UnitObject>();
            if (unit != null)
                return $"{unit.GetType().Name}:{source.name}";

            return $"{source.GetType().Name}:{source.name}";
        }
    }
}
