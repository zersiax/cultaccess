using System.Collections.Generic;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using UnityEngine;

namespace CultAccess.Status
{
    /// <summary>
    /// Announces the outcome of preaching a sermon.
    ///
    /// A sermon is one of the few actions in the game whose entire result is delivered
    /// visually. Three things happen at once and none of them speak: a
    /// <c>SermonXPOverlay</c> lists every attending follower with a "+N" for their level, a
    /// <c>UIDoctrineBar</c> fills with doctrine XP in the chosen category and shows its own
    /// "+N" increase counter beside the doctrine icon, and if the bar fills the player
    /// upgrade tree opens to spend what was earned. Before this, the only speech at that
    /// moment was the altar menu refocusing on "Sermon, button", which sounds exactly like
    /// nothing having happened.
    ///
    /// Doctrine XP is the resource this feeds. It is what the bar counts, and filling the
    /// bar is what opens the upgrade tree behind the altar's Crown option.
    ///
    /// One summary is spoken, at the moment the game commits the new total, rather than a
    /// line per follower. Attendance is captured when the overlay is built and held until
    /// then, because the two halves of the result arrive from different objects.
    /// </summary>
    [HarmonyPatch]
    internal static class SermonAnnouncer
    {
        /// <summary>
        /// The game stores doctrine XP in tenths and displays it multiplied by ten, so every
        /// spoken number here is the displayed one rather than the stored fraction.
        /// </summary>
        private const float DisplayScale = 10f;

        private static int _attendance;
        private static float _attendanceAt = float.NegativeInfinity;

        /// <summary>How long captured attendance stays relevant to a doctrine total.</summary>
        private const float AttendanceValidSeconds = 60f;

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(src.UI.Overlays.SermonXPOverlay.UISermonXPOverlayController),
            nameof(src.UI.Overlays.SermonXPOverlay.UISermonXPOverlayController.Show))]
        private static void AfterOverlayShown(List<FollowerBrain> followers)
        {
            _attendance = followers?.Count ?? 0;
            _attendanceAt = Time.unscaledTime;
            Plugin.Log.LogInfo($"[sermon] overlay attendance={_attendance}");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DoctrineUpgradeSystem), nameof(DoctrineUpgradeSystem.SetXPBySermon))]
        private static void BeforeXpSet(SermonCategory sermonCategory, ref float __state) =>
            __state = DoctrineUpgradeSystem.GetXPBySermon(sermonCategory);

        /// <summary>
        /// Hooked on the write rather than on the bar's own Show or Hide. Both of those are
        /// coroutines, so a patch on them runs when the enumerator is created rather than
        /// when the animation settles, and the stored total is not final until here.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DoctrineUpgradeSystem), nameof(DoctrineUpgradeSystem.SetXPBySermon))]
        private static void AfterXpSet(SermonCategory sermonCategory, float Value, float __state)
        {
            if (!Plugin.SpeechEnabled.Value || !Plugin.AnnounceSermonResult.Value) return;

            var before = Mathf.RoundToInt(__state * DisplayScale);
            var after = Mathf.RoundToInt(Value * DisplayScale);
            var target = Mathf.RoundToInt(
                DoctrineUpgradeSystem.GetXPTargetBySermon(sermonCategory) * DisplayScale);

            var parts = new List<string>(4) { Category(sermonCategory) };

            // A total that fell rather than rose is the bar resetting after it filled, which
            // is the good outcome: the upgrade tree is about to open to spend it.
            if (after < before)
            {
                parts.Add("complete, choose an upgrade");
            }
            else
            {
                var gained = after - before;
                if (gained > 0) parts.Add($"plus {gained}");
                if (target > 0) parts.Add($"{after} of {target} toward the next upgrade");
            }

            var attendance = Attendance();
            if (attendance > 0)
                parts.Add(attendance == 1 ? "1 follower attended" : $"{attendance} followers attended");

            var spoken = string.Join(", ", parts.ToArray());
            Plugin.Log.LogInfo(
                $"[sermon] category={sermonCategory} before={before} after={after} " +
                $"target={target} attendance={attendance} spoken=\"{spoken}\"");
            Speaker.Say(spoken, SpeechPriority.Queued);
        }

        /// <summary>
        /// Attendance is only reported alongside a total it belongs to. Doctrine XP can be
        /// written outside a sermon, and pairing a stale follower count with such a change
        /// would state something untrue about where the XP came from.
        /// </summary>
        private static int Attendance()
        {
            if (Time.unscaledTime - _attendanceAt > AttendanceValidSeconds) return 0;

            var attendance = _attendance;
            _attendance = 0;
            _attendanceAt = float.NegativeInfinity;
            return attendance;
        }

        /// <summary>
        /// The game names sermon categories only in its own doctrine UI, and the enum names
        /// are close enough to read once split. Humanise handles the compound cases —
        /// LawAndOrder and WorkAndWorship.
        ///
        /// PlayerUpgrade is not a doctrine and must not be called one. It is the track that
        /// feeds the Lamb's own upgrade tree, and it is the category a sermon uses before any
        /// doctrine is chosen — live logs had it reading as "Player Upgrade doctrine".
        /// </summary>
        private static string Category(SermonCategory category)
        {
            switch (category)
            {
                case SermonCategory.None:
                    return "Doctrine";
                case SermonCategory.PlayerUpgrade:
                    return "Player upgrade";
                default:
                    return RichText.Humanise(category.ToString()) + " doctrine";
            }
        }
    }
}
