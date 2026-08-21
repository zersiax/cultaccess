using System;
using System.Collections.Generic;
using System.Reflection;
using CultAccess.Diagnostics;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;
using Lamb.UI.PauseDetails;
using Rewired;
using src.Extensions;
using src.UI.Items;
using UnityEngine;
using UnityEngine.UI;

namespace CultAccess.UI
{
    /// <summary>Reads quest cards from their objective models, including tracking state.</summary>
    internal static class QuestMenuDescriber
    {
        private static readonly FieldInfo ActiveDataField = AccessTools.Field(
            typeof(QuestItemBase<ObjectivesData>), "_datas");
        private static readonly FieldInfo InactiveDataField = AccessTools.Field(
            typeof(QuestItemBase<ObjectivesDataFinalized>), "_datas");
        private static readonly FieldInfo CompletedContainerField = AccessTools.Field(
            typeof(QuestsMenu), "_completedQuestsContent");
        private static readonly FieldInfo FailedContainerField = AccessTools.Field(
            typeof(QuestsMenu), "_failedQuestsContent");

        internal static bool TryDescribe(Selectable selectable, out string description)
        {
            description = string.Empty;
            if (selectable == null) return false;

            var active = selectable.GetComponentInParent<QuestItemActive>();
            if (active != null)
            {
                description = DescribeActive(active);
                return true;
            }

            var inactive = selectable.GetComponentInParent<QuestItemInactive>();
            if (inactive != null)
            {
                description = DescribeInactive(inactive);
                return true;
            }

            return false;
        }

        internal static bool TryDescribeContext(Selectable selectable, out string context)
        {
            context = string.Empty;
            if (selectable?.GetComponentInParent<QuestsMenu>() == null) return false;

            context = $"Quest log. Direction keys review quests. {Key(38, "confirm")} toggles " +
                      $"tracking for active quests. Up to three can be tracked. " +
                      $"{Key(43, "previous page")} and {Key(44, "next page")} switch player pages";
            return true;
        }

        internal static void AnnounceTrackingChange(
            QuestItemActive item, List<string> previouslyTracked)
        {
            var data = ActiveData(item);
            if (data.Count == 0 || !Plugin.SpeechEnabled.Value) return;

            var title = GroupName(data[0].GroupId);
            var tracked = ObjectiveManager.IsTracked(data[0].UniqueGroupID);
            var spoken = tracked ? $"Tracking {title}" : $"Stopped tracking {title}";
            if (tracked && previouslyTracked != null && previouslyTracked.Count >= 3 &&
                !previouslyTracked.Contains(data[0].UniqueGroupID))
            {
                foreach (var oldGroup in previouslyTracked)
                {
                    if (DataManager.Instance.TrackedObjectiveGroupIDs.Contains(oldGroup)) continue;
                    spoken += $". Stopped tracking {QuestNameForUniqueId(oldGroup)} because " +
                              "only 3 quests can be tracked";
                    break;
                }
            }
            Plugin.Log.LogInfo(
                $"[quest menu] event=tracking group='{Safe(data[0].UniqueGroupID)}' " +
                $"tracked={tracked} spoken=\"{Safe(spoken)}\"");
            Speaker.Say(spoken, SpeechPriority.Now);
        }

        private static string DescribeActive(QuestItemActive item)
        {
            var data = ActiveData(item);
            if (data.Count == 0) return "Active quest details unavailable";

            var parts = new List<string>
            {
                GroupName(data[0].GroupId),
                "active quest",
                ObjectiveManager.IsTracked(data[0].UniqueGroupID) ? "tracked" : "not tracked",
            };

            foreach (var objective in data)
            {
                if (objective == null) continue;
                var text = RichText.Clean(objective.Text);
                if (text.Length == 0) continue;
                parts.Add(objective.IsComplete ? $"Completed: {text}" : text);
                if (objective.HasExpiry)
                {
                    var remaining = Mathf.RoundToInt(
                        Mathf.Clamp01(objective.ExpiryTimeNormalized) * 100f);
                    parts.Add($"time remaining {remaining} percent");
                }
            }

            AddPosition(parts, item, active: true);
            parts.Add(item.Button.Confirmable
                ? $"{Key(38, "confirm")} toggles tracking"
                : "tracking unavailable");
            return string.Join(". ", parts.ToArray());
        }

        private static string DescribeInactive(QuestItemInactive item)
        {
            var data = InactiveData(item);
            if (data.Count == 0) return "Past quest details unavailable";

            var menu = item.GetComponentInParent<QuestsMenu>();
            var failed = IsInside(item.transform, ReadContainer(FailedContainerField, menu));
            var completed = IsInside(item.transform, ReadContainer(CompletedContainerField, menu));
            var state = failed ? "failed quest" : completed ? "completed quest" : "past quest";
            var parts = new List<string> { GroupName(data[0].GroupId), state };

            foreach (var objective in data)
            {
                if (objective == null) continue;
                var text = RichText.Clean(objective.GetText());
                if (text.Length > 0) parts.Add(text);
            }

            AddPosition(parts, item, active: false, failed: failed);
            parts.Add("read only");
            return string.Join(". ", parts.ToArray());
        }

        private static List<ObjectivesData> ActiveData(QuestItemActive item)
        {
            try
            {
                return ActiveDataField?.GetValue(item) as List<ObjectivesData> ??
                       new List<ObjectivesData>();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read active quest data: {e.Message}");
                return new List<ObjectivesData>();
            }
        }

        private static List<ObjectivesDataFinalized> InactiveData(QuestItemInactive item)
        {
            try
            {
                return InactiveDataField?.GetValue(item) as List<ObjectivesDataFinalized> ??
                       new List<ObjectivesDataFinalized>();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read past quest data: {e.Message}");
                return new List<ObjectivesDataFinalized>();
            }
        }

        private static void AddPosition(
            List<string> parts, Component target, bool active, bool failed = false)
        {
            var menu = target.GetComponentInParent<QuestsMenu>();
            if (menu == null) return;

            if (active)
            {
                var items = menu.GetComponentsInChildren<QuestItemActive>(false);
                var index = Array.IndexOf(items, target as QuestItemActive);
                if (index >= 0)
                    parts.Add(ProgressionText.Position("active quest", index, items.Length));
                return;
            }

            var container = ReadContainer(
                failed ? FailedContainerField : CompletedContainerField, menu);
            if (container == null) return;
            var past = container.GetComponentsInChildren<QuestItemInactive>(false);
            var pastIndex = Array.IndexOf(past, target as QuestItemInactive);
            if (pastIndex >= 0)
                parts.Add(ProgressionText.Position(
                    $"{(failed ? "failed" : "completed")} quest", pastIndex, past.Length));
        }

        private static RectTransform ReadContainer(FieldInfo field, QuestsMenu menu)
        {
            if (menu == null || field == null) return null;
            try { return field.GetValue(menu) as RectTransform; }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read quest list container: {e.Message}");
                return null;
            }
        }

        private static bool IsInside(Transform child, Transform parent) =>
            child != null && parent != null && child.IsChildOf(parent);

        private static string GroupName(string groupId)
        {
            var name = RichText.Clean(groupId.TryLocalize());
            if (RichText.IsUsableLocalization(name, groupId)) return name;

            var fallback = groupId ?? string.Empty;
            var slash = fallback.LastIndexOf('/');
            if (slash >= 0 && slash + 1 < fallback.Length)
                fallback = fallback.Substring(slash + 1);
            return RichText.Humanise(fallback);
        }

        private static string QuestNameForUniqueId(string uniqueGroupId)
        {
            if (DataManager.Instance?.Objectives != null)
            {
                foreach (var objective in DataManager.Instance.Objectives)
                {
                    if (objective != null && objective.UniqueGroupID == uniqueGroupId)
                        return GroupName(objective.GroupId);
                }
            }
            return "the oldest tracked quest";
        }

        private static string Key(int action, string fallback)
        {
            var key = BindingDump.KeyboardBindingsForAction(action);
            return key.Length > 0 ? key : fallback;
        }

        private static string Safe(string value) =>
            string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\r", " ").Replace("\n", " ").Replace("'", string.Empty);
    }

    [HarmonyPatch(typeof(QuestItemActive), "OnClicked")]
    internal static class QuestTrackingPatch
    {
        [HarmonyPrefix]
        private static void BeforeTrackingChanged(out List<string> __state)
        {
            var tracked = DataManager.Instance?.TrackedObjectiveGroupIDs;
            __state = tracked == null ? new List<string>() : new List<string>(tracked);
        }

        [HarmonyPostfix]
        private static void AfterTrackingChanged(
            QuestItemActive __instance, List<string> __state) =>
            QuestMenuDescriber.AnnounceTrackingChange(__instance, __state);
    }
}
