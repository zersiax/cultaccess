using CultAccess.Speech;
using UnityEngine;

namespace CultAccess.Status
{
    /// <summary>
    /// Announces chore level-ups, the progression behind the mop.
    ///
    /// Chores — cleaning burnt food, waste and the like — call
    /// <c>PlayerChoreXPBarController.AddChoreXP</c>, which fills a bar and, at a threshold,
    /// runs a level-up animation and upgrades the Lamb's mop. The whole system is visual: a
    /// bar, an animation and a changed tool sprite, with nothing spoken and no notification
    /// card. A player reported an unexplained "something was gained or upgraded" animation
    /// after cleaning food, and this was it.
    ///
    /// Only the level-up is announced. Individual XP awards fire on every mess cleaned and
    /// would be pure noise; the level is the part that changes anything.
    ///
    /// Polled rather than hooked because <c>ChoreXPLevel</c> is incremented from three
    /// separate places, one of them inside a coroutine that runs after the animation. One
    /// integer read is cheaper than three fragile patches.
    /// </summary>
    internal static class ChoreProgressTracker
    {
        /// <summary>Matches the onboarding poll; a level-up is not a per-frame event.</summary>
        private const float PollSeconds = 0.25f;

        /// <summary>
        /// <c>PlayerChoreXPBarController.Play</c> stops awarding at this level, and
        /// <c>DataManager.TargetChoreXP</c> holds ten thresholds.
        /// </summary>
        private const int MaxLevel = 9;

        private static float _nextPollAt;
        private static bool _hasBaseline;
        private static int _lastLevel;
        private static DataManager _observed;

        internal static void Tick()
        {
            if (Time.unscaledTime < _nextPollAt) return;
            _nextPollAt = Time.unscaledTime + PollSeconds;

            try
            {
                var data = DataManager.Instance;
                if (data == null) return;

                var level = data.ChoreXPLevel;

                // A loaded save arrives at whatever level it reached; announcing that would
                // narrate history rather than an event.
                if (!_hasBaseline || !ReferenceEquals(data, _observed))
                {
                    _observed = data;
                    _lastLevel = level;
                    _hasBaseline = true;
                    Plugin.Log.LogInfo($"[chore] baseline level={level}");
                    return;
                }

                if (level <= _lastLevel)
                {
                    _lastLevel = level;
                    return;
                }

                var previous = _lastLevel;
                _lastLevel = level;

                var spoken = level >= MaxLevel
                    ? $"Chore level {level}, fully upgraded"
                    : $"Chore level {level}";
                Plugin.Log.LogInfo(
                    $"[chore] level {previous}->{level} xp={data.ChoreXP:0.##} " +
                    $"target={data.GetTargetChoreXP:0.##} spoken=\"{spoken}\"");

                if (Plugin.SpeechEnabled.Value && Plugin.AnnounceChoreProgress.Value)
                    Speaker.Say(spoken, SpeechPriority.Queued);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not read chore progress: {e.Message}");
            }
        }

        /// <summary>Level and progress toward the next one, for the where-am-I key.</summary>
        internal static string CurrentStatus()
        {
            try
            {
                var data = DataManager.Instance;
                if (data == null) return string.Empty;

                var level = data.ChoreXPLevel;
                if (level >= MaxLevel) return $"chore level {level}, fully upgraded";

                return $"chore level {level}, {data.ChoreXP:0.#} of " +
                       $"{data.GetTargetChoreXP:0.#}";
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Could not read chore progress: {e.Message}");
                return string.Empty;
            }
        }

        internal static void Shutdown()
        {
            _hasBaseline = false;
            _observed = null;
            _nextPollAt = 0f;
        }
    }
}
