using System;
using System.Collections.Generic;
using System.Globalization;
using CultAccess.Speech;
using CultAccess.Status;
using CultAccess.Util;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace CultAccess.UI
{
    /// <summary>
    /// Reads the game's transient notification cards after their own localization code
    /// has produced the final text. Item counters are also announced; their own redraw
    /// cadence coalesces rapid pickups of the same resource before we speak the update.
    /// </summary>
    [HarmonyPatch]
    internal static class NotificationAnnouncer
    {
        private sealed class PendingItemChange
        {
            public InventoryItem.ITEM_TYPE Type;
            public int Delta;
            public int Total;
            public float DueAt;
        }

        /// <summary>
        /// How long a burst is allowed to keep growing before it is spoken as one line. The
        /// game's own card coalesces redraws on a 0.2s cadence, so this sits above that.
        /// </summary>
        private const float SettleSeconds = 0.45f;

        private static readonly AccessTools.FieldRef<NotificationBase, TextMeshProUGUI> DescriptionRef =
            AccessTools.FieldRefAccess<NotificationBase, TextMeshProUGUI>("_description");

        /// <summary>
        /// The faith card's numeric change, which the player sees as an arrow icon and a
        /// coloured number in <c>_faithDeltaText</c> — a field outside <c>_description</c>,
        /// which is why the reason was reaching the reader without the amount.
        /// </summary>
        private static readonly AccessTools.FieldRef<NotificationFaith, float> FaithDeltaRef =
            AccessTools.FieldRefAccess<NotificationFaith, float>("_faithDelta");
        private static readonly Dictionary<NotificationItem, PendingItemChange> PendingItems =
            new Dictionary<NotificationItem, PendingItemChange>();
        private static readonly List<NotificationItem> DueItems = new List<NotificationItem>();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NotificationBase), "Configure", new[] { typeof(NotificationBase.Flair) })]
        private static void AfterConfigure(NotificationBase __instance)
        {
            if (!Plugin.SpeechEnabled.Value || !Plugin.AnnounceNotifications.Value) return;
            if (__instance == null) return;
            if (__instance is NotificationItem) return;

            Announce(__instance);
        }

        /// <summary>
        /// The first pickup of a burst. <c>Configure</c> assigns the card's running delta and
        /// every later pickup on the same card accumulates through <c>UpdateDelta</c>, so this
        /// opens the pending change rather than speaking.
        ///
        /// Announcing here as well as in <see cref="Tick"/> split every burst into two lines:
        /// live logs show "Gained 1 Coin, 17 total" immediately followed by "Gained 2 Coin, 19
        /// total", and "Gained 1 Berry, 16 total" followed by "Gained 4 Berry, 20 total" — the
        /// first item spoken alone, then the remainder as a second sentence.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NotificationItem), nameof(NotificationItem.Configure),
            new[] { typeof(InventoryItem.ITEM_TYPE), typeof(int), typeof(NotificationBase.Flair) })]
        private static void AfterItemConfigured(
            NotificationItem __instance, InventoryItem.ITEM_TYPE itemType, int delta)
        {
            if (!Plugin.SpeechEnabled.Value || !Plugin.AnnounceNotifications.Value) return;
            if (__instance == null) return;

            // Cards come from a pool. A pending change still sitting on this one belongs to
            // the notification being replaced, and is owed to the player before the new one
            // starts; dropping it would lose a pickup outright.
            if (PendingItems.TryGetValue(__instance, out var superseded))
            {
                PendingItems.Remove(__instance);
                AnnounceItem(superseded.Type, superseded.Delta, superseded.Total);
            }

            PendingItems[__instance] = new PendingItemChange
            {
                Type = itemType,
                Delta = delta,
                Total = Inventory.GetItemQuantity((int)itemType),
                DueAt = Time.unscaledTime + SettleSeconds,
            };
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NotificationItem), nameof(NotificationItem.UpdateDelta))]
        private static void AfterItemDelta(NotificationItem __instance, int delta)
        {
            if (!Plugin.SpeechEnabled.Value || !Plugin.AnnounceNotifications.Value) return;
            if (__instance == null) return;

            if (!PendingItems.TryGetValue(__instance, out var pending))
            {
                pending = new PendingItemChange { Type = __instance.ItemType };
                PendingItems.Add(__instance, pending);
            }

            pending.Delta += delta;
            pending.Total = Inventory.GetItemQuantity((int)pending.Type);
            pending.DueAt = Time.unscaledTime + SettleSeconds;
        }

        internal static void Tick()
        {
            if (PendingItems.Count == 0) return;

            DueItems.Clear();
            foreach (var pair in PendingItems)
            {
                if (Time.unscaledTime >= pair.Value.DueAt)
                    DueItems.Add(pair.Key);
            }

            foreach (var item in DueItems)
            {
                var pending = PendingItems[item];
                PendingItems.Remove(item);
                if (Plugin.SpeechEnabled.Value && Plugin.AnnounceNotifications.Value)
                    AnnounceItem(pending.Type, pending.Delta, pending.Total);
            }
        }

        private static void Announce(NotificationBase notification)
        {
            if (notification == null) return;

            TextMeshProUGUI description;
            try
            {
                description = DescriptionRef(notification);
            }
            catch
            {
                return;
            }

            var text = RichText.Clean(description?.text);
            if (text.Length == 0) return;
            if (text.IndexOf("This is a Notification", StringComparison.OrdinalIgnoreCase) >= 0) return;
            if (text.StartsWith("MISSING LOCALISATION", StringComparison.OrdinalIgnoreCase)) return;

            // Measured 2026-08-25: the game's own descriptions already end in a full stop,
            // and several end in an ellipsis, so appending produced "Your flock grows...."
            // and "You gave a Sermon..". Trim before joining rather than after.
            var faith = FaithChange(notification);
            if (faith.Length > 0)
                text = $"{RichText.TrimTrailingPunctuation(text)}. {faith}";

            Plugin.Log.LogInfo($"[notification] {notification.GetType().Name}: \"{text}\"");
            Speaker.Say(text, SpeechPriority.Queued);
        }

        /// <summary>
        /// The amount of faith a card carries, and the level it leaves the cult at.
        ///
        /// The card's reason and its number live in two different fields, and only the reason
        /// was being read — so "a follower has died" arrived without the ten points of faith
        /// that went with it. One real-world event should produce one sentence naming both,
        /// which is why this is folded into the existing announcement rather than spoken
        /// separately.
        ///
        /// Empty for the many cards that carry no faith change at all: the game passes a delta
        /// of zero for purely informational ones such as two followers starting a fight, and
        /// hides the icon and number when it does.
        /// </summary>
        private static string FaithChange(NotificationBase notification)
        {
            if (!(notification is NotificationFaith card)) return string.Empty;

            try
            {
                // NotificationFaith backs three templates. Only the faith one is reachable in
                // this build — the sin and warmth entry points have no callers, and
                // WarmthBar.ModifyWarmth is an empty method — but a card whose key says warmth
                // would be a different bar, so it is named in the log and left alone rather
                // than reported as faith.
                var key = card.LocKey ?? string.Empty;
                if (key.EndsWith("/Warmth", StringComparison.Ordinal))
                {
                    Plugin.Log.LogInfo($"[notification] warmth card, delta not spoken key={key}");
                    return string.Empty;
                }

                // Mirrors NotificationFaith.Configure, which zeroes the displayed delta while
                // the cult is brainwashed. Speaking the pre-lock number would report a change
                // the player's own screen says did not happen.
                if (FollowerBrainStats.BrainWashed)
                    return Localization.Strings.Get("cult.faith_locked_no_change");

                var delta = FaithDeltaRef(card);
                if (Mathf.Approximately(delta, 0f)) return string.Empty;

                var level = CultStatusText.Percent(CultFaithManager.CultFaithNormalised);
                var amount = Mathf.Abs(delta).ToString("0.##", CultureInfo.InvariantCulture);
                return Localization.Strings.Format(
                    delta > 0f ? "cult.faith_rose" : "cult.faith_fell", amount, level);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read a faith notification's delta: {e.Message}");
                return string.Empty;
            }
        }

        private static void AnnounceItem(InventoryItem.ITEM_TYPE type, int delta, int total)
        {
            if (delta == 0) return;

            var localizedName = InventoryItem.LocalizedName(type);
            var name = RichText.IsUsableLocalization(localizedName, $"Inventory/{type}")
                ? RichText.Clean(localizedName)
                : RichText.Humanise(type.ToString());

            var verb = delta > 0 ? "Gained" : "Lost";
            var text = $"{verb} {Math.Abs(delta)} {name}, {total} total";
            Plugin.Log.LogInfo($"[item notification] type={type} delta={delta} total={total}: \"{text}\"");
            Speaker.Say(text, SpeechPriority.Queued);
        }
    }
}
