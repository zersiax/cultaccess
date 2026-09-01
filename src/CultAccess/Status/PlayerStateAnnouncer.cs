using System;
using System.Collections.Generic;
using CultAccess.Speech;
using UnityEngine;

namespace CultAccess.Status
{
    /// <summary>
    /// Mirrors the player's live heart and fervour HUD. Property hooks provide the change
    /// signal; values are read again when speech is due so the announced total is never a
    /// stale intermediate value from a multi-setter damage or pickup sequence.
    /// </summary>
    internal static class PlayerStateAnnouncer
    {
        private sealed class PendingHealthChange
        {
            public HealthPlayer Target;
            public HealthSnapshot Before;
            public float DueAt;

            /// <summary>
            /// Set when something arrived that the combat rate limit must not sit on.
            ///
            /// The limit folds changes together for up to two and a half seconds, and a fold
            /// reports the net. A blue heart picked up while a drop was waiting therefore
            /// cancelled out against it and the pair was announced as nothing at all — the
            /// player heard silence where they had just gained a heart. Deferring is only
            /// acceptable while every change in the window is more of the same.
            /// </summary>
            public bool NeverDefer;
        }

        private sealed class PendingFervourChange
        {
            public FaithAmmo Target;
            public float Before;
            public float DueAt;
        }

        // Damage and heart effects frequently update several heart pools in one call.
        // Waiting for the same quiet interval used by objective changes produces one final read.
        private const float SettleSeconds = 0.35f;

        /// <summary>
        /// Minimum gap between spoken health drops while a fight is running.
        ///
        /// The settle window above coalesces one damage event, not a fight. In the room where
        /// a run ended, nine separate "Health dropped to..." lines went out at interrupting
        /// priority inside about forty seconds, plus three fervour lines — a screen reader
        /// talking continuously over the only cues that could have saved the run, all of it
        /// reporting a number the player could ask for at any time.
        ///
        /// Deferring rather than dropping: the pending entry is pushed out instead of
        /// removed, so nothing is lost and the sentence that eventually goes out carries the
        /// current total rather than a backlog of stale ones.
        /// </summary>
        private const float CombatHealthInterval = 2.5f;

        /// <summary>
        /// Health at or below one heart, in the game's units of two HP per heart, always
        /// speaks immediately. That is the reading a player changes their behaviour on, and
        /// it is the one moment where interrupting a cue is the right trade.
        /// </summary>
        private const float CriticalHealth = 2f;

        private static float _lastCombatHealthAt = float.NegativeInfinity;

        private static readonly Dictionary<HealthPlayer, PendingHealthChange> PendingHealth =
            new Dictionary<HealthPlayer, PendingHealthChange>();
        private static readonly Dictionary<FaithAmmo, PendingFervourChange> PendingFervour =
            new Dictionary<FaithAmmo, PendingFervourChange>();
        private static readonly HashSet<HealthPlayer> InitializingHealth =
            new HashSet<HealthPlayer>();
        private static readonly List<HealthPlayer> ReadyHealth = new List<HealthPlayer>();
        private static readonly List<FaithAmmo> ReadyFervour = new List<FaithAmmo>();

        internal static HealthSnapshot Capture(HealthPlayer health)
        {
            if (health == null) return default;
            return new HealthSnapshot(
                health.HP,
                health.totalHP,
                health.SpiritHearts,
                health.TotalSpiritHearts,
                health.BlueHearts,
                health.BlackHearts,
                health.FireHearts,
                health.IceHearts);
        }

        internal static void BeginHealthInitialization(HealthPlayer health)
        {
            if (health == null) return;
            InitializingHealth.Add(health);
            PendingHealth.Remove(health);
        }

        internal static void EndHealthInitialization(HealthPlayer health)
        {
            if (health == null) return;
            InitializingHealth.Remove(health);
            PendingHealth.Remove(health);
        }

        internal static void ForgetHealth(HealthPlayer health)
        {
            if (health == null) return;
            InitializingHealth.Remove(health);
            PendingHealth.Remove(health);
        }

        internal static void HealthChanged(HealthPlayer health, HealthSnapshot before)
        {
            if (health == null || InitializingHealth.Contains(health)) return;
            if (!ShouldQueue(health.playerFarming)) return;

            var after = Capture(health);
            if (PlayerStatusText.HealthChange(before, after).Length == 0) return;

            if (PendingHealth.TryGetValue(health, out var pending))
            {
                pending.DueAt = Time.unscaledTime + SettleSeconds;

                // Anything that is not a further drop stops this being deferrable. Gains and
                // capacity changes are rare, always worth hearing, and are exactly what a
                // netting fold would erase.
                if (!(after.Current < before.Current)) pending.NeverDefer = true;
                return;
            }

            PendingHealth.Add(health, new PendingHealthChange
            {
                Target = health,
                Before = before,
                DueAt = Time.unscaledTime + SettleSeconds,
            });
        }

        internal static void HealthPickupCompleted(
            HealthPlayer health,
            PlayerFarming player,
            HealthSnapshot before,
            string pickupName)
        {
            if (health == null || !ShouldQueue(player)) return;

            var after = Capture(health);
            var spoken = PlayerStatusText.HealthPickup(pickupName, before, after);
            if (spoken.Length == 0) return;

            // The heart setters have already queued the ordinary state-change wording.
            // Replace that pending entry with one semantic acquisition announcement.
            PendingHealth.Remove(health);
            spoken = WithPlayer(player, spoken);
            Plugin.Log.LogInfo(
                $"[player state] type=heart-pickup player={PlayerName(player)} " +
                $"pickup=\"{pickupName}\" before={before.Current:0.##}/{before.Capacity:0.##} " +
                $"after={after.Current:0.##}/{after.Capacity:0.##} spoken=\"{spoken}\"");
            Speaker.Say(spoken, SpeechPriority.Queued);
        }

        internal static void FervourChanged(FaithAmmo ammo, float before)
        {
            if (ammo == null || !ShouldQueue(ammo.playerFarming)) return;
            if (Math.Abs(ammo.Ammo - before) < 0.001f) return;

            if (PendingFervour.TryGetValue(ammo, out var pending))
            {
                pending.DueAt = Time.unscaledTime + SettleSeconds;
                return;
            }

            PendingFervour.Add(ammo, new PendingFervourChange
            {
                Target = ammo,
                Before = before,
                DueAt = Time.unscaledTime + SettleSeconds,
            });
        }

        internal static void ForgetFervour(FaithAmmo ammo)
        {
            if (ammo != null) PendingFervour.Remove(ammo);
        }

        internal static void Tick()
        {
            if (!Plugin.SpeechEnabled.Value || !Plugin.AnnouncePlayerStateChanges.Value)
            {
                PendingHealth.Clear();
                PendingFervour.Clear();
                return;
            }

            ReadyHealth.Clear();
            foreach (var pair in PendingHealth)
            {
                if (Time.unscaledTime >= pair.Value.DueAt) ReadyHealth.Add(pair.Key);
            }

            foreach (var key in ReadyHealth)
            {
                var pending = PendingHealth[key];
                PendingHealth.Remove(key);
                var target = pending.Target;
                if (target == null || !ShouldQueue(target.playerFarming)) continue;

                var after = Capture(target);
                var spoken = PlayerStatusText.HealthChange(pending.Before, after);
                if (spoken.Length == 0) continue;

                var dropped = after.Current < pending.Before.Current;
                if (dropped && !pending.NeverDefer && Combat.CombatLifecycle.InCombat &&
                    after.Current > CriticalHealth &&
                    Time.unscaledTime - _lastCombatHealthAt < CombatHealthInterval)
                {
                    // Put it back rather than discard it. Further hits fold into the same
                    // entry, and when it does go out it states where the health actually is.
                    pending.DueAt = _lastCombatHealthAt + CombatHealthInterval;
                    PendingHealth[key] = pending;

                    // Logged, because a deferral that leaves no trace is indistinguishable in
                    // a session log from a limiter that never fired — which is exactly the
                    // question the last log could not answer about this code.
                    Plugin.Log.LogInfo(
                        $"[player state] health deferred to={after.Current:0.##}/{after.Capacity:0.##} " +
                        $"sinceLast={Time.unscaledTime - _lastCombatHealthAt:0.00}s");
                    continue;
                }

                // Only combat drops arm the limiter, so a quiet-world scratch
                // cannot defer the first real report of the next fight.
                if (dropped && Combat.CombatLifecycle.InCombat)
                    _lastCombatHealthAt = Time.unscaledTime;

                spoken = WithPlayer(target.playerFarming, spoken);
                Plugin.Log.LogInfo(
                    $"[player state] type=health player={PlayerName(target.playerFarming)} " +
                    $"before={pending.Before.Current:0.##}/{pending.Before.Capacity:0.##} " +
                    $"after={after.Current:0.##}/{after.Capacity:0.##} spoken=\"{spoken}\"");
                var priority = after.Current < pending.Before.Current
                    ? SpeechPriority.Now
                    : SpeechPriority.Queued;
                Speaker.Say(spoken, priority);
            }

            ReadyFervour.Clear();
            foreach (var pair in PendingFervour)
            {
                if (Time.unscaledTime >= pair.Value.DueAt) ReadyFervour.Add(pair.Key);
            }

            foreach (var key in ReadyFervour)
            {
                var pending = PendingFervour[key];
                PendingFervour.Remove(key);
                var target = pending.Target;
                if (target == null || !ShouldQueue(target.playerFarming)) continue;

                var current = target.Ammo;
                var total = target.Total;
                var cost = CurseCost(target.playerFarming);
                var spoken = PlayerStatusText.FervourChange(
                    pending.Before, current, total, cost);
                if (spoken.Length == 0) continue;

                spoken = WithPlayer(target.playerFarming, spoken);
                Plugin.Log.LogInfo(
                    $"[player state] type=fervour player={PlayerName(target.playerFarming)} " +
                    $"before={pending.Before:0.##} after={current:0.##} total={total:0.##} " +
                    $"curseCost={cost:0.##} spoken=\"{spoken}\"");
                Speaker.Say(spoken, SpeechPriority.Queued);
            }
        }

        internal static void AnnounceCurrent()
        {
            var player = CurrentPlayer();
            if (player == null || player.health == null)
            {
                Speaker.Say("Player status unavailable");
                return;
            }

            var health = PlayerStatusText.HealthStatus(Capture(player.health));
            var fervour = CurrentFervourStatus(player);
            var tarotCount = player.RunTrinkets?.Count ?? 0;
            var tarot = tarotCount == 1
                ? "1 tarot card active"
                : $"{tarotCount} tarot cards active";

            // Appended rather than led with, so the familiar survival readout still comes
            // first mid-combat. SayParts drops empty entries silently.
            var time = DayCycleAnnouncer.CurrentStatus();
            var step = OnboardingTracker.CurrentStep();

            // Only ever a few words, and only when a cult bar is actually low. The full
            // four-bar reading has its own key precisely because this one is already long
            // and is pressed constantly; what belongs here is the fact that something is
            // wrong, which a sighted player has on screen the whole time.
            var cult = Plugin.AnnounceCultInWhereAmI.Value
                ? CultStatusAnnouncer.AlertClause()
                : string.Empty;

            Speaker.SayParts(SpeechPriority.Now, health, fervour, tarot, time, step, cult);
        }

        internal static void Shutdown()
        {
            PendingHealth.Clear();
            PendingFervour.Clear();
            InitializingHealth.Clear();
            ReadyHealth.Clear();
            ReadyFervour.Clear();
        }

        private static string CurrentFervourStatus(PlayerFarming player)
        {
            try
            {
                if (DataManager.Instance == null || !DataManager.Instance.EnabledSpells)
                    return "Fervour not unlocked";

                var ammo = player.playerSpells?.faithAmmo;
                if (ammo == null) return "Fervour unavailable";

                var unlimited =
                    (SettingsManager.Settings?.Accessibility?.UnlimitedFervour ?? false) ||
                    (player.playerRelic != null && player.playerRelic.UnlimitedFervour);
                return PlayerStatusText.FervourStatus(
                    ammo.Ammo, ammo.Total, CurseCost(player), unlimited);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read current fervour: {e.Message}");
                return "Fervour unavailable";
            }
        }

        private static float CurseCost(PlayerFarming player)
        {
            try { return player?.playerSpells?.AmmoCost ?? 0f; }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read curse cost: {e.Message}");
                return 0f;
            }
        }

        private static bool ShouldQueue(PlayerFarming player)
        {
            if (player == null || !Plugin.SpeechEnabled.Value ||
                !Plugin.AnnouncePlayerStateChanges.Value)
                return false;

            if (PlayerFarming.players != null && PlayerFarming.players.Contains(player))
                return true;
            return PlayerFarming.Instance == player;
        }

        private static PlayerFarming CurrentPlayer()
        {
            try
            {
                var navigator = MonoSingleton<src.UINavigator.UINavigatorNew>.Instance;
                if (navigator?.AllowInputOnlyFromPlayer != null)
                    return navigator.AllowInputOnlyFromPlayer;
            }
            catch
            {
                // Menus are optional; the main player remains the world-status authority.
            }

            return PlayerFarming.Instance;
        }

        private static string WithPlayer(PlayerFarming player, string text) =>
            PlayerFarming.playersCount > 1 ? $"{PlayerName(player)}, {text}" : text;

        private static string PlayerName(PlayerFarming player) =>
            player == null ? "unknown" : player.isLamb ? "Lamb" : "Goat";
    }

}
