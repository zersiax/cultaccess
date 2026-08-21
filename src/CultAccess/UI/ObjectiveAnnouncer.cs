using System;
using System.Collections.Generic;
using CultAccess.Speech;
using CultAccess.Util;
using UnityEngine;

namespace CultAccess.UI
{
    /// <summary>
    /// Mirrors the objective cards driven by <see cref="ObjectiveManager"/>. Changes wait
    /// for a short quiet period so synchronous event pairs and multi-item pickups become
    /// one useful announcement instead of a burst of intermediate counters.
    /// </summary>
    internal static class ObjectiveAnnouncer
    {
        private enum ChangeKind
        {
            Updated,
            Added,
            Completed,
            Failed,
        }

        private sealed class PendingChange
        {
            public ObjectivesData Objective;
            public ChangeKind Kind;
            public string Group;
            public string Text;
            public string State;
            public float DueAt;
            public long Sequence;
        }

        /// <summary>
        /// A single harvest can update the same objective over several adjacent frames.
        /// This matches the settling interval already used by other animated UI readers.
        /// </summary>
        private const float SettleSeconds = 0.35f;

        private static readonly Dictionary<ObjectivesData, PendingChange> Pending =
            new Dictionary<ObjectivesData, PendingChange>();
        private static readonly Dictionary<ObjectivesData, string> LastStates =
            new Dictionary<ObjectivesData, string>();
        private static readonly List<PendingChange> Ready = new List<PendingChange>();

        private static bool _subscribed;
        private static long _nextSequence;

        internal static void Initialize()
        {
            if (_subscribed) return;

            ObjectiveManager.OnObjectiveAdded += OnAdded;
            ObjectiveManager.OnObjectiveUpdated += OnUpdated;
            ObjectiveManager.OnObjectiveCompleted += OnCompleted;
            ObjectiveManager.OnObjectiveFailed += OnFailed;
            _subscribed = true;
        }

        internal static void Shutdown()
        {
            if (_subscribed)
            {
                ObjectiveManager.OnObjectiveAdded -= OnAdded;
                ObjectiveManager.OnObjectiveUpdated -= OnUpdated;
                ObjectiveManager.OnObjectiveCompleted -= OnCompleted;
                ObjectiveManager.OnObjectiveFailed -= OnFailed;
            }

            _subscribed = false;
            Pending.Clear();
            LastStates.Clear();
            Ready.Clear();
        }

        internal static void Tick()
        {
            if (Pending.Count == 0) return;
            if (!Plugin.SpeechEnabled.Value)
            {
                Pending.Clear();
                return;
            }

            Ready.Clear();
            foreach (var change in Pending.Values)
            {
                if (Time.unscaledTime >= change.DueAt)
                    Ready.Add(change);
            }

            Ready.Sort((left, right) => left.Sequence.CompareTo(right.Sequence));
            foreach (var change in Ready)
            {
                Pending.Remove(change.Objective);

                if (change.Kind == ChangeKind.Updated &&
                    LastStates.TryGetValue(change.Objective, out var previous) &&
                    previous == change.State)
                    continue;

                var spoken = Format(change);
                Plugin.Log.LogInfo(
                    $"[objective] event={change.Kind.ToString().ToLowerInvariant()} " +
                    $"id={change.Objective.ID} group=\"{change.Group}\" " +
                    $"text=\"{change.Text}\" spoken=\"{spoken}\"");
                Speaker.Say(spoken, SpeechPriority.Queued);

                if (change.Kind == ChangeKind.Completed || change.Kind == ChangeKind.Failed)
                    LastStates.Remove(change.Objective);
                else
                    LastStates[change.Objective] = change.State;
            }
        }

        private static void OnAdded(ObjectivesData objective) =>
            Queue(objective, ChangeKind.Added);

        private static void OnUpdated(ObjectivesData objective) =>
            Queue(objective, ChangeKind.Updated);

        private static void OnCompleted(ObjectivesData objective) =>
            Queue(objective, ChangeKind.Completed);

        private static void OnFailed(ObjectivesData objective) =>
            Queue(objective, ChangeKind.Failed);

        private static void Queue(ObjectivesData objective, ChangeKind kind)
        {
            if (objective == null || !Plugin.SpeechEnabled.Value) return;

            var group = ReadGroup(objective);
            var text = ReadText(objective);
            var state = $"{group}\n{text}\n{objective.IsComplete}\n{objective.IsFailed}";

            if (Pending.TryGetValue(objective, out var pending))
            {
                if (Rank(kind) > Rank(pending.Kind)) pending.Kind = kind;
                pending.Group = group;
                pending.Text = text;
                pending.State = state;
                pending.DueAt = Time.unscaledTime + SettleSeconds;
                return;
            }

            Pending.Add(objective, new PendingChange
            {
                Objective = objective,
                Kind = kind,
                Group = group,
                Text = text,
                State = state,
                DueAt = Time.unscaledTime + SettleSeconds,
                Sequence = _nextSequence++,
            });
        }

        private static int Rank(ChangeKind kind)
        {
            switch (kind)
            {
                case ChangeKind.Failed: return 3;
                case ChangeKind.Completed: return 3;
                case ChangeKind.Added: return 2;
                default: return 1;
            }
        }

        private static string Format(PendingChange change)
        {
            var prefix = change.Kind switch
            {
                ChangeKind.Added => "New objective",
                ChangeKind.Completed => "Objective complete",
                ChangeKind.Failed => "Objective failed",
                _ => "Objective updated",
            };

            if (change.Group.Length == 0 || change.Group == change.Text)
                return $"{prefix}: {change.Text}";
            return $"{prefix}, {change.Group}: {change.Text}";
        }

        private static string ReadGroup(ObjectivesData objective)
        {
            var raw = objective.GroupId ?? string.Empty;
            try
            {
                var localized = raw.TryLocalize();
                if (RichText.IsUsableLocalization(localized, raw))
                    return RichText.Clean(localized);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning(
                    $"Could not localize objective group '{raw}' for {objective.ID}: {e.Message}");
            }

            var separator = raw.LastIndexOf('/');
            return RichText.Humanise(separator >= 0 ? raw.Substring(separator + 1) : raw);
        }

        private static string ReadText(ObjectivesData objective)
        {
            try
            {
                var text = RichText.Clean(objective.Text);
                if (RichText.IsUsableLocalization(text)) return text;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning(
                    $"Could not read objective text for {objective.ID} ({objective.Type}): {e.Message}");
            }

            return "Objective " + RichText.Humanise(objective.Type.ToString());
        }
    }
}
