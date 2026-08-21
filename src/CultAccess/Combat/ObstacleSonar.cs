using CultAccess.Audio;
using CultAccess.Navigation;
using UnityEngine;

using Compass = CultAccess.Navigation.Compass;

namespace CultAccess.Combat
{
    /// <summary>
    /// Warns only about solid geometry in the player's current movement corridor. A full
    /// ambient wall map would mask combat audio; this answers the actionable question,
    /// "will continuing in this direction hit something?"
    /// </summary>
    internal static class ObstacleSonar
    {
        // These are perceptual repeat timings, not game mechanics. They are deliberately
        // named and isolated so the play log can drive tuning after the first sound test.
        private const float FarRepeatSeconds = 0.8f;
        private const float NearRepeatSeconds = 0.2f;
        private const float BlockedRepeatSeconds = 0.65f;
        private const float ThreatSuppressionSeconds = 0.3f;
        private const float ContactToleranceMultiplier = 2f;
        private const float DirectionChangedDot = 0.92f;

        private static float _nextCueAt;
        private static bool _wasBlocked;
        private static int _lastColliderId;
        private static Vector2 _lastDirection;

        internal static void Tick(PlayerFarming player)
        {
            if (player == null || player.playerController == null || player.state == null)
            {
                ResetContact();
                return;
            }

            var state = player.state.CURRENT_STATE;
            if (state == StateMachine.State.Dodging || !IsWalkingState(state) ||
                player.GoToAndStopping)
            {
                ResetContact();
                return;
            }

            var controller = player.playerController;
            var direction = new Vector2(controller.xDir, controller.yDir);
            if (direction.sqrMagnitude <=
                PlayerController.MinInputForMovement * PlayerController.MinInputForMovement)
            {
                ResetContact();
                return;
            }

            direction.Normalize();
            if (_lastDirection != Vector2.zero &&
                Vector2.Dot(direction, _lastDirection) < DirectionChangedDot)
            {
                _nextCueAt = 0f;
                _wasBlocked = false;
            }
            _lastDirection = direction;

            var collider = player.circleCollider2D;
            if (collider == null || !collider.enabled)
            {
                ResetContact();
                return;
            }

            // The game's live dodge speed/duration provides a meaningful warning range:
            // geometry inside this corridor can stop the next roll.
            var warningDistance = Mathf.Max(
                ColliderGeometry.Radius(collider),
                controller.DodgeSpeed * controller.DodgeDuration);

            if (!ObstacleProbe.TryFind(player, collider, direction, warningDistance, out var hit))
            {
                ResetContact();
                return;
            }

            var colliderId = hit.collider.GetInstanceID();
            var contactTolerance = Physics2D.defaultContactOffset * ContactToleranceMultiplier;
            var blocked = hit.distance <= contactTolerance;
            var changedObstacle = colliderId != _lastColliderId;
            var now = Time.unscaledTime;

            if (blocked && (!_wasBlocked || changedObstacle || now >= _nextCueAt))
            {
                var cuePosition = PositionAlong(
                    player.transform.position, direction, warningDistance);
                if (!CuePlayer.CriticalRecently(ThreatSuppressionSeconds) &&
                    CuePlayer.Play(CueId.WallBlocked, cuePosition))
                {
                    Log("blocked", hit, warningDistance, direction);
                }
                _nextCueAt = now + BlockedRepeatSeconds;
            }
            else if (!blocked && now >= _nextCueAt &&
                     !CuePlayer.CriticalRecently(ThreatSuppressionSeconds))
            {
                var cuePosition = PositionAlong(
                    player.transform.position, direction, warningDistance);
                var closeness = 1f - Mathf.Clamp01(hit.distance / warningDistance);
                if (CuePlayer.Play(
                        CueId.WallNear, cuePosition, Mathf.Lerp(0.55f, 1f, closeness)))
                {
                    Log("near", hit, warningDistance, direction);
                }
                _nextCueAt = now + Mathf.Lerp(FarRepeatSeconds, NearRepeatSeconds, closeness);
            }

            _wasBlocked = blocked;
            _lastColliderId = colliderId;
        }

        internal static void OnDodge(PlayerFarming player)
        {
            if (player == null || player.playerController == null) return;

            var controller = player.playerController;
            var radians = controller.forceDir * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
            var corridorLength = Mathf.Max(
                ColliderGeometry.Radius(player.circleCollider2D),
                controller.speed * controller.DodgeDuration);
            var hadInput = Mathf.Abs(controller.xDir) > PlayerController.MinInputForMovement ||
                           Mathf.Abs(controller.yDir) > PlayerController.MinInputForMovement;

            var blocked = ObstacleProbe.TryFind(
                player, player.circleCollider2D, direction, corridorLength, out var hit);
            var target = PositionAlong(player.transform.position, direction, corridorLength);
            CueId? kind = blocked
                ? CueId.DodgeBlocked
                : hadInput ? (CueId?)null : CueId.DodgeDirection;
            var cuePlayed = kind.HasValue && CuePlayer.Play(kind.Value, target);

            var bearing = "unknown";
            if (Compass.TryDescribe(player.transform.position, target, out var described, out _))
                bearing = described;

            CombatDiagnostics.Info(
                $"[combat dodge] direction={bearing} angle={controller.forceDir:0.0} " +
                $"source={(hadInput ? "movement-input" : "facing")} " +
                $"speed={controller.speed:0.00} corridor={corridorLength:0.00} " +
                $"cue={(kind.HasValue ? kind.Value.ToString() : "none")} " +
                $"cuePlayed={cuePlayed} " +
                (blocked
                    ? $"blocked=true obstacle={Describe(hit.collider)} distance={hit.distance:0.00}"
                    : "blocked=false"));

            ResetContact();
        }

        internal static void Reset()
        {
            _nextCueAt = 0f;
            ResetContact();
        }

        private static bool IsWalkingState(StateMachine.State state) =>
            state == StateMachine.State.Idle ||
            state == StateMachine.State.Moving ||
            state == StateMachine.State.Idle_Winter ||
            state == StateMachine.State.Moving_Winter;

        private static Vector3 PositionAlong(Vector3 origin, Vector2 direction, float distance) =>
            origin + new Vector3(direction.x, direction.y, 0f) *
            Mathf.Max(distance, Physics2D.defaultContactOffset);

        private static void Log(
            string kind,
            RaycastHit2D hit,
            float warningDistance,
            Vector2 direction)
        {
            CombatDiagnostics.Info(
                $"[combat wall] kind={kind} obstacle={Describe(hit.collider)} " +
                $"layer={LayerMask.LayerToName(hit.collider.gameObject.layer)} " +
                $"distance={hit.distance:0.00} warningRange={warningDistance:0.00} " +
                $"direction=({direction.x:0.00},{direction.y:0.00})");
        }

        private static string Describe(Collider2D collider) =>
            collider == null
                ? "none"
                : $"{collider.GetType().Name}:{collider.gameObject.name}";

        private static void ResetContact()
        {
            _nextCueAt = 0f;
            _wasBlocked = false;
            _lastColliderId = 0;
            _lastDirection = Vector2.zero;
        }
    }
}
