using CultAccess.Speech;
using UnityEngine;

namespace CultAccess.Status
{
    /// <summary>
    /// Reports what the game is currently waiting for.
    ///
    /// <c>DataManager.CurrentOnboardingPhase</c> (also exposed as
    /// <c>Onboarding.CurrentPhase</c>) is the game's own authoritative answer to "what am I
    /// supposed to do now". It matters because onboarding beats are condition-polled rather
    /// than position-triggered: <c>Onboarding</c> waits for the player to be in the base and
    /// idle, then plays a conversation. Nothing about that is discoverable by walking around,
    /// and while a beat holds <c>DataManager.AllowBuilding</c> false the target list loses
    /// every label, so the scanner cannot stand in for this.
    ///
    /// Polled rather than hooked: there is no change event, and the setter is not the only
    /// writer of the underlying field. A quarter-second poll of one already-loaded field is
    /// cheaper than the hierarchy search a missed hook would cost us.
    /// </summary>
    internal static class OnboardingTracker
    {
        /// <summary>
        /// Poll interval. Fast enough that the step is current when the player asks, slow
        /// enough to stay off the per-frame path. Not derived from game data; it is a
        /// sampling choice.
        /// </summary>
        private const float PollSeconds = 0.25f;

        private static float _nextPollAt;
        private static DataManager _observed;
        private static DataManager.OnboardingPhase _lastPhase;
        private static bool _hasBaseline;

        internal static void Tick()
        {
            if (Time.unscaledTime < _nextPollAt) return;
            _nextPollAt = Time.unscaledTime + PollSeconds;

            DataManager manager;
            DataManager.OnboardingPhase phase;
            try
            {
                manager = DataManager.Instance;
                if (manager == null) return;
                phase = manager.CurrentOnboardingPhase;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not read onboarding phase: {e.Message}");
                return;
            }

            // A newly loaded save arrives mid-onboarding. Establish its phase silently, the
            // same way restored tarot cards and load-time health setters are suppressed;
            // narrating the current step during a load screen is noise, not information.
            if (!_hasBaseline || !ReferenceEquals(manager, _observed))
            {
                _observed = manager;
                _lastPhase = phase;
                _hasBaseline = true;
                Plugin.Log.LogInfo($"[onboarding] baseline phase={phase}");
                return;
            }

            if (phase == _lastPhase) return;

            var previous = _lastPhase;
            _lastPhase = phase;

            var spoken = ResolveStep(manager, phase);
            Plugin.Log.LogInfo(
                $"[onboarding] phase {previous}->{phase} spoken=\"{spoken}\"");

            if (spoken.Length == 0) return;
            if (!Plugin.SpeechEnabled.Value || !Plugin.AnnounceOnboardingPhase.Value) return;

            // Queued: a phase almost always changes during or immediately after the
            // conversation that caused it, and cutting that off would lose the story.
            Speaker.Say(spoken, SpeechPriority.Queued);
        }

        /// <summary>Current step for the where-am-I key, or empty when nothing is pending.</summary>
        internal static string CurrentStep()
        {
            try
            {
                var manager = DataManager.Instance;
                if (manager == null) return string.Empty;
                return ResolveStep(manager, manager.CurrentOnboardingPhase);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not read onboarding phase: {e.Message}");
                return string.Empty;
            }
        }

        internal static void Shutdown()
        {
            _observed = null;
            _hasBaseline = false;
            _nextPollAt = 0f;
        }

        private static string ResolveStep(
            DataManager manager,
            DataManager.OnboardingPhase phase)
        {
            var hasPendingRecruit = manager.Followers_Recruit != null &&
                                    manager.Followers_Recruit.Count > 0;
            var isShrineRecruitObjective = hasPendingRecruit &&
                                           ObjectiveManager.HasCustomObjectiveOfType(
                                               Objectives.CustomQuestTypes.IndoctrinateNewRecruit);
            return OnboardingText.NextStep(phase.ToString(), isShrineRecruitObjective);
        }
    }
}
