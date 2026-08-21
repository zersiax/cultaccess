using System.Collections.Generic;
using CultAccess.Speech;
using UnityEngine;

namespace CultAccess.Combat
{
    /// <summary>Announces only the boundaries of a fight, never every spawn or death.</summary>
    internal static class CombatLifecycle
    {
        private const float CountSettleSeconds = 0.35f;

        private static readonly HashSet<int> Seen = new HashSet<int>();
        private static int _observedCount = -1;
        private static float _countChangedAt;
        private static bool _combatActive;
        private static bool _initialized;

        internal static bool Announce = true;

        internal static void Initialize()
        {
            if (_initialized) return;
            RoomLockController.OnRoomCleared += OnRoomCleared;
            _initialized = true;
        }

        internal static void Shutdown()
        {
            if (_initialized)
                RoomLockController.OnRoomCleared -= OnRoomCleared;
            _initialized = false;
            Reset();
        }

        internal static void Tick(PlayerFarming player)
        {
            if (player == null || !LocationManager.IsDungeonActive())
            {
                Reset();
                return;
            }

            var count = CountAliveHostiles();
            if (count != _observedCount)
            {
                CombatDiagnostics.Info(
                    $"[combat state] enemies {_observedCount}->{count} active={_combatActive}");
                _observedCount = count;
                _countChangedAt = Time.unscaledTime;
            }

            if (Time.unscaledTime - _countChangedAt < CountSettleSeconds) return;
            if (!PlayerCanReceiveCombatContext(player)) return;

            if (!_combatActive && count > 0)
            {
                _combatActive = true;
                if (Announce && Plugin.SpeechEnabled != null && Plugin.SpeechEnabled.Value)
                    Speaker.Say(count == 1 ? "Combat, 1 enemy." : $"Combat, {count} enemies.");
            }
        }

        internal static void Reset()
        {
            _observedCount = -1;
            _countChangedAt = 0f;
            _combatActive = false;
            Seen.Clear();
        }

        private static int CountAliveHostiles()
        {
            Seen.Clear();
            var count = Count(Health.team2);
            count += Count(Health.dangerousAnimals);
            return count;
        }

        private static int Count(List<Health> source)
        {
            if (source == null) return 0;

            var count = 0;
            for (var i = 0; i < source.Count; i++)
            {
                var health = source[i];
                if (health == null || !health.gameObject.activeInHierarchy) continue;
                if (health.CurrentHP <= 0f || !Seen.Add(health.GetInstanceID())) continue;
                count++;
            }
            return count;
        }

        private static bool PlayerCanReceiveCombatContext(PlayerFarming player)
        {
            if (player.GoToAndStopping || player.state == null) return false;

            switch (player.state.CURRENT_STATE)
            {
                case StateMachine.State.InActive:
                case StateMachine.State.CustomAnimation:
                case StateMachine.State.SpawnIn:
                case StateMachine.State.Dieing:
                case StateMachine.State.Dead:
                case StateMachine.State.GameOver:
                case StateMachine.State.FinalGameOver:
                    return false;
                default:
                    return true;
            }
        }

        private static void OnRoomCleared()
        {
            CombatDiagnostics.Info(
                $"[combat state] authoritative-room-clear active={_combatActive}");
            if (!_combatActive) return;

            _combatActive = false;
            _observedCount = 0;
            _countChangedAt = Time.unscaledTime;
            if (Announce && Plugin.SpeechEnabled != null && Plugin.SpeechEnabled.Value)
                Speaker.Say("Room clear.");
        }
    }
}
