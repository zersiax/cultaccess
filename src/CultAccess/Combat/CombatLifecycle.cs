using System.Collections.Generic;
using CultAccess.Speech;
using UnityEngine;

namespace CultAccess.Combat
{
    /// <summary>Announces only the boundaries of a fight, never every spawn or death.</summary>
    internal static class CombatLifecycle
    {
        private const float CountSettleSeconds = 0.35f;

        /// <summary>
        /// How long a leader encounter has to stay empty before its clear is believed.
        ///
        /// `RoomLockController.OnRoomCleared` is the game's own event and it is not what its
        /// name suggests. `DungeonLeaderMechanics` raises it at every stage boundary of a
        /// miniboss encounter — once when its rounds combat finishes, again when the spawned
        /// followers are all dead, and so on. A session log caught three inside one fight:
        /// "Combat, 2 enemies." then "Room clear." then "Combat, 4 enemies." then
        /// "Room clear." then "Combat, 1 enemy." then "Room clear."
        ///
        /// A sighted player never sees the event; it drives music, lighting and the chest
        /// reveal, and the next wave is visibly on its way. Spoken, it is the single most
        /// dangerous thing this mod can say, because it means "stop fighting, go find the
        /// exit" and it was arriving twice per encounter while the fight was still on.
        ///
        /// `doorsDown` looked like the discriminator and is not — both miniboss calls pass
        /// true. So the clear is confirmed by observation instead: hold it, and believe it
        /// only if the room stays empty. Three seconds comfortably covers the gap between
        /// stages, and a few seconds of delay in a miniboss room costs far less than a lie.
        /// </summary>
        private const float LeaderClearConfirmSeconds = 3f;

        private static readonly HashSet<int> Seen = new HashSet<int>();
        private static int _observedCount = -1;
        private static float _countChangedAt;
        private static bool _combatActive;
        private static bool _clearPending;
        private static float _clearPendingAt;
        private static bool _initialized;
        private static Navigation.TargetCategory? _categoryBeforeCombat;

        /// <summary>
        /// Point the target list at the enemies while a fight is on, and put it back after.
        ///
        /// The alternative designs both cost more. A separate combat mode for the controller
        /// D-pad spends one of about fifteen buttons and adds a mode to track by ear at the
        /// exact moment that is hardest. Silently remapping the D-pad during combat gives the
        /// same press two meanings. Switching the *category* instead leaves every control
        /// meaning exactly what it always means: "enemies" is already one of the nine filters,
        /// stepping it already works, and the player can still reach any other filter mid-fight
        /// because nothing is locked.
        /// </summary>
        internal static bool FocusEnemiesInCombat = true;

        internal static bool Announce = true;

        /// <summary>
        /// Whether a fight is currently running. Read by the health announcer, which coarsens
        /// its reporting while this holds so it cannot talk over the cues the fight depends on.
        /// Stays true across the wave gaps of a leader encounter, which is the point.
        /// </summary>
        internal static bool InCombat => _combatActive;

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

            if (_clearPending)
            {
                if (count > 0)
                {
                    // The next stage landed. Combat never actually stopped, so this is a new
                    // wave rather than a new fight — but the count is what the player needs,
                    // and it is the same sentence they already know.
                    _clearPending = false;
                    CombatDiagnostics.Info(
                        $"[combat state] clear cancelled; {count} more hostile(s) arrived");
                    SpeakCount(count);
                    return;
                }

                if (Time.unscaledTime - _clearPendingAt >= LeaderClearConfirmSeconds)
                    AnnounceClear("confirmed-quiet");
                return;
            }

            if (!_combatActive && count > 0)
            {
                _combatActive = true;
                FocusEnemies();
                SpeakCount(count);
            }
        }

        private static void SpeakCount(int count)
        {
            if (!Announce || Plugin.SpeechEnabled == null || !Plugin.SpeechEnabled.Value) return;

            var counted = count == 1 ? "Combat, 1 enemy." : $"Combat, {count} enemies.";

            // Folded into the sentence that was going out anyway rather than spoken as its
            // own line. The switch is only worth a word, and combat start is the worst
            // possible moment to spend two announcements on one event.
            Speaker.Say(_categoryBeforeCombat.HasValue
                ? counted + " Targets: enemies."
                : counted);
        }

        private static void FocusEnemies()
        {
            if (!FocusEnemiesInCombat) return;

            var current = Navigation.TargetCatalog.Category;
            if (current == Navigation.TargetCategory.Enemies) return;

            _categoryBeforeCombat = current;
            Navigation.TargetCatalog.SelectCategory(Navigation.TargetCategory.Enemies);
        }

        private static void RestoreCategory()
        {
            if (!_categoryBeforeCombat.HasValue) return;

            var restore = _categoryBeforeCombat.Value;
            _categoryBeforeCombat = null;

            // Only if the player has not since chosen something for themselves. Overriding a
            // deliberate choice made during the fight would be worse than leaving the filter
            // where they put it.
            if (Navigation.TargetCatalog.Category == Navigation.TargetCategory.Enemies)
                Navigation.TargetCatalog.SelectCategory(restore);

            // A cleared room is a changed world — the enemies are corpses, barriers have
            // opened and a chest may have appeared — so the scan behind every filter is now
            // about a room that no longer exists. Without this the restored filter was applied
            // to that stale scan and reported nothing at all.
            Navigation.Navigator.MarkCatalogueStale("room-cleared");
        }

        internal static void Reset()
        {
            _observedCount = -1;
            _countChangedAt = 0f;
            _combatActive = false;
            _clearPending = false;
            _clearPendingAt = 0f;
            _categoryBeforeCombat = null;
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
            var leaderEncounter = DungeonLeaderMechanics.Instance != null;
            CombatDiagnostics.Info(
                $"[combat state] authoritative-room-clear active={_combatActive} " +
                $"leaderEncounter={leaderEncounter} alreadyPending={_clearPending}");
            if (!_combatActive) return;

            if (leaderEncounter)
            {
                // Held rather than spoken. Instance is set once when the encounter starts and
                // nulled once when it ends, so this is true for every stage boundary inside
                // it — including the real last one, which simply arrives a few seconds later
                // once the room has stayed empty.
                _clearPending = true;
                _clearPendingAt = Time.unscaledTime;
                return;
            }

            AnnounceClear("room-cleared-event");
        }

        private static void AnnounceClear(string reason)
        {
            _combatActive = false;
            _clearPending = false;
            RestoreCategory();
            _observedCount = 0;
            _countChangedAt = Time.unscaledTime;

            CombatDiagnostics.Info($"[combat state] room-clear announced reason={reason}");
            if (Announce && Plugin.SpeechEnabled != null && Plugin.SpeechEnabled.Value)
                Speaker.Say("Room clear.");
        }
    }
}
