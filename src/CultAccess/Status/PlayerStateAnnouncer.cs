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

            Speaker.SayParts(SpeechPriority.Now, health, fervour, tarot, time, step);
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
