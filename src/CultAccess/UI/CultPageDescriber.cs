using System.Collections.Generic;
using CultAccess.Localization;
using CultAccess.Status;
using CultAccess.Util;
using Lamb.UI;

namespace CultAccess.UI
{
    /// <summary>
    /// Reads the two pages that describe the cult itself: the Cult tab of the player menu, and
    /// the Cult page behind the Temple altar's Doctrine menu.
    ///
    /// Both share one defect, and it is worse than the usual icon-only one. Their contents are
    /// plain <c>TMP_Text</c> that belongs to no control at all — nine bare numbers on the
    /// statistics board, each labelled only by the picture beside it, and four cult bars drawn
    /// as fills. Only the two buttons on the Doctrine page are <c>Selectable</c>, so focus
    /// cannot reach the numbers even to read them badly, and the generic panel reader would
    /// recite them as a string of digits with nothing to say which is which.
    ///
    /// So the values are taken from <c>DataManager</c> rather than from the labels. That is
    /// both simpler than reflecting nine private fields and the right way round: the page is a
    /// view of that data, and reading the source cannot pick up a stale or half-animated
    /// string.
    /// </summary>
    internal static class CultPageDescriber
    {
        private static readonly HarmonyLib.AccessTools.FieldRef<HistoricalNotificationFaith, TMPro.TextMeshProUGUI>
            HistoryDeltaRef = SafeField<HistoricalNotificationFaith, TMPro.TextMeshProUGUI>(
                "_faithDeltaText");

        /// <summary>
        /// A row of the Cult tab's notification history.
        ///
        /// Measured 2026-08-25: these read as <c>"5, You have a new Follower. Your flock
        /// grows..."</c> — the bare faith number first, unlabelled, before the sentence that
        /// gives it meaning. A listener hears the same first word on every row and has to wait
        /// through it to reach the part that differs, which is the ordering rule this project
        /// keeps rediscovering. The number is moved behind the text and named.
        /// </summary>
        internal static bool TryDescribeHistoryRow(
            UnityEngine.UI.Selectable selectable, out string description)
        {
            description = string.Empty;
            if (selectable == null) return false;

            var row = selectable.GetComponentInParent<HistoricalNotificationFaith>();
            if (row == null) return false;

            var delta = string.Empty;
            var text = string.Empty;
            try
            {
                var deltaText = HistoryDeltaRef == null ? null : HistoryDeltaRef(row);
                var raw = RichText.Clean(deltaText == null ? null : deltaText.text);

                // Collect the row's prose, skipping the delta field itself so it is not said
                // twice once it has been moved to the end and labelled.
                var parts = new List<string>(2);
                foreach (var tmp in row.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    if (ReferenceEquals(tmp, deltaText)) continue;
                    var clean = RichText.Clean(tmp.text);
                    if (clean.Length > 0 && !parts.Contains(clean)) parts.Add(clean);
                }

                text = string.Join(", ", parts.ToArray());
                delta = FaithDelta(raw);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[cult page] could not read a history row: {e.Message}");
            }

            description = delta.Length == 0
                ? text
                : text.Length == 0
                    ? delta
                    : $"{RichText.TrimTrailingPunctuation(text)}, {delta}";

            Plugin.Log.LogInfo(
                $"[cult page] row=faith-history spoken=\"{description}\"");
            return description.Length > 0;
        }

        /// <summary>
        /// The row's own displayed number, turned into words. The sign is what the arrow
        /// beside it shows, so it is named rather than read as a minus character.
        /// </summary>
        private static string FaithDelta(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            var trimmed = raw.Trim();
            var negative = trimmed.StartsWith("-");
            var digits = trimmed.TrimStart('+', '-').Trim();
            if (digits.Length == 0) return string.Empty;

            foreach (var c in digits)
                if (!char.IsDigit(c) && c != '.' && c != ',') return string.Empty;

            return Strings.Format(
                negative ? "cult.faith_history_down" : "cult.faith_history_up", digits);
        }

        /// <summary>
        /// Claim the panel if either page is the open one, or is the active tab inside it.
        /// A tab set keeps its other pages in the hierarchy but disabled, so the search is
        /// deliberately restricted to active objects: that is what picks the page the player
        /// is actually looking at out of the several that exist.
        /// </summary>
        internal static bool TryDescribePanel(UIMenuBase menu, out string description)
        {
            description = string.Empty;
            if (menu == null) return false;

            var cultPage = menu as DoctrineCultPage ??
                           menu.GetComponentInChildren<DoctrineCultPage>(false);
            if (cultPage != null)
            {
                description = DescribeStatistics();
                Plugin.Log.LogInfo($"[cult page] page=doctrine spoken=\"{description}\"");
                return true;
            }

            var cultMenu = menu as CultMenu ?? menu.GetComponentInChildren<CultMenu>(false);
            if (cultMenu != null)
            {
                description = DescribeCultTab();
                Plugin.Log.LogInfo($"[cult page] page=cult-tab spoken=\"{description}\"");
                return true;
            }

            return false;
        }

        /// <summary>
        /// The Cult tab: the cult's name, its four bars, and its population. The notification
        /// history below it is left alone deliberately — those rows each carry their own
        /// <c>Selectable</c>, so focus already reads them and repeating them here would make
        /// one keypress recite the entire history of the save.
        /// </summary>
        private static string DescribeCultTab()
        {
            var parts = new List<string>(4);

            var name = CultName();
            if (name.Length > 0) parts.Add(name);

            var snapshot = CultStatusAnnouncer.Snapshot();
            if (snapshot != null) parts.Add(CultStatusText.Status(snapshot.Value));

            var homes = Homes();
            if (homes >= 0)
                parts.Add(Strings.Plural("cult.home", "cult.homes", homes));

            return parts.Count == 0 ? Strings.Get("cult.unavailable") : string.Join(" ", parts.ToArray());
        }

        /// <summary>
        /// The statistics board. Every one of these is a bare number on screen with a picture
        /// beside it, so each is named here; the winters line follows the game's own gate and
        /// is absent outside a save where seasons run.
        /// </summary>
        private static string DescribeStatistics()
        {
            var data = DataManager.Instance;
            if (data == null) return Strings.Get("cult.unavailable");

            var parts = new List<string>(11);

            // Measured 2026-08-26: this read "visionlessCult., 7 followers ever" — the name
            // template carries its own full stop for the Cult tab, which joins with spaces,
            // and this list joins with commas. Same defect I had just fixed elsewhere, made
            // again one function away. Trim at the join rather than owning a second template.
            var name = RichText.TrimTrailingPunctuation(CultName());
            if (name.Length > 0) parts.Add(name);

            try
            {
                var living = data.Followers?.Count ?? 0;
                var dead = data.Followers_Dead?.Count ?? 0;
                parts.Add(Strings.Format("cult.stat_total_followers", living + dead));
                parts.Add(Strings.Format("cult.stat_murders", data.STATS_Murders));
                parts.Add(Strings.Format(
                    "cult.stat_starved", data.STATS_FollowersStarvedToDeath));
                parts.Add(Strings.Format("cult.stat_sacrifices", data.STATS_Sacrifices));
                parts.Add(Strings.Format("cult.stat_natural_deaths", data.STATS_NaturalDeaths));
                parts.Add(Strings.Format("cult.stat_crusades", data.dungeonRun));
                parts.Add(Strings.Format("cult.stat_player_deaths", data.playerDeaths));
                parts.Add(Strings.Format("cult.stat_kills", data.KillsInGame));

                if (SeasonsManager.Active)
                    parts.Add(Strings.Format("cult.stat_winters", data.WintersOccured));
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[cult page] could not read the statistics: {e.Message}");
            }

            return string.Join(", ", parts.ToArray()) + ".";
        }

        private static HarmonyLib.AccessTools.FieldRef<TOwner, TField> SafeField<TOwner, TField>(
            string name)
        {
            try { return HarmonyLib.AccessTools.FieldRefAccess<TOwner, TField>(name); }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning(
                    $"[cult page] {typeof(TOwner).Name}.{name} not found: {e.Message}");
                return null;
            }
        }

        private static string CultName()
        {
            try
            {
                var name = RichText.Clean(DataManager.Instance?.CultName);
                return name.Length == 0 ? string.Empty : Strings.Format("cult.named", name);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[cult page] could not read the cult name: {e.Message}");
                return string.Empty;
            }
        }

        /// <summary>Homes rather than every structure, which is what the tab counts.</summary>
        private static int Homes()
        {
            try { return StructureManager.GetTotalHomesCount(); }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[cult page] could not count homes: {e.Message}");
                return -1;
            }
        }
    }
}
