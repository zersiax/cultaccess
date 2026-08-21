using System.Collections.Generic;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;
using Lamb.UI.DeathScreen;
using TMPro;
using UnityEngine.UI;

namespace CultAccess.UI
{
    /// <summary>
    /// Builds the run summary represented by the death screen's prose, stat icons,
    /// route nodes and loot icons. It is read when the Continue button receives focus,
    /// which is the game's own signal that the animated summary has finished populating.
    /// </summary>
    internal static class DeathScreenAnnouncer
    {
        private static readonly AccessTools.FieldRef<UIDeathScreenOverlayController, TextMeshProUGUI> TitleRef =
            AccessTools.FieldRefAccess<UIDeathScreenOverlayController, TextMeshProUGUI>("_title");
        private static readonly AccessTools.FieldRef<UIDeathScreenOverlayController, TextMeshProUGUI> SubtitleRef =
            AccessTools.FieldRefAccess<UIDeathScreenOverlayController, TextMeshProUGUI>("_subTitle");
        private static readonly AccessTools.FieldRef<UIDeathScreenOverlayController, TextMeshProUGUI> TimeRef =
            AccessTools.FieldRefAccess<UIDeathScreenOverlayController, TextMeshProUGUI>("_timeText");
        private static readonly AccessTools.FieldRef<UIDeathScreenOverlayController, TextMeshProUGUI> KillsRef =
            AccessTools.FieldRefAccess<UIDeathScreenOverlayController, TextMeshProUGUI>("_killsText");
        private static readonly AccessTools.FieldRef<UIDeathScreenOverlayController, UIDeathScreenOverlayController.Results> ResultRef =
            AccessTools.FieldRefAccess<UIDeathScreenOverlayController, UIDeathScreenOverlayController.Results>("_result");
        private static readonly AccessTools.FieldRef<UIDeathScreenOverlayController, int> LevelsRef =
            AccessTools.FieldRefAccess<UIDeathScreenOverlayController, int>("_levels");
        private static readonly AccessTools.FieldRef<UIDeathScreenOverlayController, List<DeathScreenInventoryItem>> ItemsRef =
            AccessTools.FieldRefAccess<UIDeathScreenOverlayController, List<DeathScreenInventoryItem>>("inventoryItems");

        private static UIDeathScreenOverlayController _lastAnnounced;

        /// <summary>Return a summary once for the first focused control on each results screen.</summary>
        public static bool TryDescribeFirst(Selectable selectable, out string summary)
        {
            summary = string.Empty;
            if (selectable == null) return false;

            var screen = selectable.GetComponentInParent<UIDeathScreenOverlayController>();
            if (screen == null || screen == _lastAnnounced) return false;

            summary = Describe(screen);
            if (summary.Length == 0) return false;

            _lastAnnounced = screen;
            return true;
        }

        public static string Describe(UIDeathScreenOverlayController screen)
        {
            if (screen == null) return string.Empty;

            var parts = new List<string>();
            AddUnique(parts, TitleRef(screen)?.text);
            AddUnique(parts, SubtitleRef(screen)?.text);

            var result = ResultRef(screen);
            var time = RichText.Clean(TimeRef(screen)?.text);
            var kills = RichText.Clean(KillsRef(screen)?.text);

            if (result == UIDeathScreenOverlayController.Results.GameOver)
            {
                if (time.Length > 0) parts.Add(time);
                if (kills.Length > 0) parts.Add($"{kills} followers remaining");
            }
            else
            {
                if (time.Length > 0) parts.Add($"run time {time}");
                if (kills.Length > 0) parts.Add($"{kills} kills");

                var visited = DataManager.Instance?.dungeonVisitedRooms?.Count ?? 0;
                var levels = LevelsRef(screen);
                if (visited > 0 && levels > 0)
                    parts.Add($"rooms reached {visited} of {levels}");
                else if (visited > 0)
                    parts.Add($"rooms reached {visited}");
            }

            var loot = DescribeLoot(ItemsRef(screen));
            if (loot.Length > 0) parts.Add("loot, " + loot);

            var summary = string.Join(", ", parts.ToArray());
            Plugin.Log.LogInfo($"[death screen] result={result} spoken=\"{summary}\"");
            return summary;
        }

        private static string DescribeLoot(List<DeathScreenInventoryItem> items)
        {
            if (items == null || items.Count == 0) return string.Empty;

            var parts = new List<string>();
            foreach (var item in items)
            {
                if (item == null || item.Item == null) continue;

                var localizedName = InventoryItem.LocalizedName(item.Type);
                var name = RichText.IsUsableLocalization(
                    localizedName, $"Inventory/{item.Type}")
                    ? RichText.Clean(localizedName)
                    : RichText.Humanise(item.Type.ToString());

                var entry = $"{item.Item.quantity} {name}";
                var delta = RichText.Clean(item.DeltaText?.text);
                if (delta.Length > 0) entry += $", change {delta}";
                parts.Add(entry);
            }

            return string.Join(", ", parts.ToArray());
        }

        private static void AddUnique(List<string> parts, string raw)
        {
            var clean = RichText.Clean(raw);
            if (clean.Length == 0 || parts.Contains(clean)) return;
            parts.Add(clean);
        }
    }
}
