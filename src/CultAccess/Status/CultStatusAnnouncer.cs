using System;
using CultAccess.Speech;
using UnityEngine;

namespace CultAccess.Status
{
    /// <summary>
    /// Reads the game's four cult-wide bars and reports the moment one of them crosses the
    /// line where the game starts taking followers away.
    ///
    /// Why this exists at all: faith, food, cleanliness and warmth are drawn on the HUD as
    /// fill-only images with no text anywhere, and below a quarter full the game punch-scales
    /// and pulses them. That pulse is not decoration — <c>CultFaithManager.UpdateSimulation</c>,
    /// <c>HungerBar.UpdateSimulation</c> and <c>IllnessBar.UpdateSimulation</c> each pick a
    /// random follower below that same fraction and make them a dissenter, starving, or ill.
    /// A sighted player is told attrition has begun; before this, a blind player found out
    /// when a follower died.
    ///
    /// Nothing here is cached. The bar values are read live on every tick and every keypress,
    /// per the standing rule about announcing a stale number confidently. What *is* remembered
    /// is only which side of the threshold each bar was on last time, which is a comparison
    /// rather than a value.
    /// </summary>
    internal static class CultStatusAnnouncer
    {
        /// <summary>
        /// How often the bars are sampled. The underlying simulations run on their own
        /// coroutines at 0.3 s and on an interval counter, and a crossing has no deadline
        /// attached, so a second is responsive enough and costs four property reads.
        /// </summary>
        private const float SampleInterval = 1f;

        /// <summary>
        /// Distance a bar must move back past the threshold before "recovered" is spoken.
        /// The faith drip and the waste count both hover, and without this a bar resting on
        /// exactly a quarter would alternate low and recovered indefinitely.
        /// </summary>
        private const float RecoveryMargin = 0.03f;

        private sealed class BarState
        {
            public bool Low;
            public bool Locked;
            public bool Known;

            /// <summary>
            /// Whether the drop into this state was actually spoken. Measured 2026-08-26: food
            /// crossed low away from the base, was suppressed, and the recovery back over the
            /// line was then announced on its own — so the player heard "Food back up" for a
            /// fall they were never told about, which is worse than silence. A recovery is only
            /// news to someone who heard the warning.
            /// </summary>
            public bool AnnouncedLow;
        }

        private static readonly BarState[] States =
        {
            new BarState(), new BarState(), new BarState(), new BarState(),
        };

        private static float _nextSampleAt;
        private static bool _baselined;
        private static DataManager _observed;

        /// <summary>Live snapshot, or null when the game is not far enough along to have one.</summary>
        internal static CultStatusSnapshot? Snapshot()
        {
            try
            {
                var data = DataManager.Instance;
                if (data == null) return null;

                return new CultStatusSnapshot(
                    new CultBar(
                        CultBarKind.Faith,
                        CultFaithManager.CultFaithNormalised,
                        data.ShowCultFaith,
                        FollowerBrainStats.BrainWashed),
                    new CultBar(
                        CultBarKind.Food,
                        HungerBar.HungerNormalized,
                        data.ShowCultHunger,
                        FollowerBrainStats.Fasting),
                    new CultBar(
                        CultBarKind.Cleanliness,
                        IllnessBar.IllnessNormalized,
                        data.ShowCultIllness,
                        false),
                    new CultBar(
                        CultBarKind.Warmth,
                        WarmthBar.WarmthNormalized,
                        data.ShowCultWarmth,
                        FollowerBrainStats.LockedWarmth),
                    data.Followers?.Count ?? 0,
                    data.Followers_Dead?.Count ?? 0);
            }
            catch (Exception e)
            {
                // Every one of these is a static property on a manager that may not exist yet
                // during a scene load. Report rather than swallow: a silent null here would
                // present as the feature simply never speaking.
                Plugin.Log.LogWarning($"[cult status] could not read the cult bars: {e.Message}");
                return null;
            }
        }

        /// <summary>The on-demand readout, on its own key.</summary>
        internal static void AnnounceCurrent()
        {
            var snapshot = Snapshot();
            if (snapshot == null)
            {
                Speaker.Say(Localization.Strings.Get("cult.unavailable"));
                return;
            }

            var text = CultStatusText.Status(snapshot.Value);
            Log("requested", snapshot.Value, text);
            Speaker.Say(text, SpeechPriority.Now);
        }

        /// <summary>
        /// The clause the where-am-I key appends. Empty unless a bar is actually low, so the
        /// survival readout does not grow four sentences for the ordinary case.
        /// </summary>
        internal static string AlertClause()
        {
            var snapshot = Snapshot();
            return snapshot == null ? string.Empty : CultStatusText.Alerts(snapshot.Value);
        }

        internal static void Tick()
        {
            if (!Plugin.SpeechEnabled.Value || !Plugin.AnnounceCultStatus.Value) return;
            if (Time.unscaledTime < _nextSampleAt) return;

            // Re-armed before anything that can fail or return early. A throttle set after the
            // work is not a throttle on any frame where the work bails out.
            _nextSampleAt = Time.unscaledTime + SampleInterval;

            var snapshot = Snapshot();
            if (snapshot == null) return;

            // A loaded save arrives with whatever state it was saved in. Detecting the new
            // DataManager and forgetting the old thresholds is what stops a load reporting
            // four crossings that happened before the player quit. Same mechanism
            // OnboardingTracker uses, and it shares that one's exposure: a load that reuses
            // the existing instance would go unnoticed, which the rebaseline marker in the
            // log is there to make visible if it ever happens.
            var data = DataManager.Instance;
            if (!ReferenceEquals(data, _observed))
            {
                _observed = data;
                Rebaseline("data manager changed");
            }

            var atBase = AtBase();
            var bars = snapshot.Value.Bars;
            for (var i = 0; i < bars.Length; i++)
                Evaluate(bars[i], States[i], atBase);

            if (!_baselined)
            {
                _baselined = true;
                Log("baseline", snapshot.Value, string.Empty);
            }
        }

        /// <summary>
        /// Whether the cult HUD the crossings mirror is on screen.
        ///
        /// The four bars live with the base scene, so on a crusade a sighted player is not
        /// watching one pulse — and a warning that arrived mid-fight would be both noise and
        /// more than they get. Faith and illness attrition can still fire out there once the
        /// tutorial is past, though, so a crossing suppressed here is recorded in the log
        /// rather than dropped silently: that marker is what will say whether anything is
        /// actually being lost.
        /// </summary>
        private static bool AtBase()
        {
            try { return PlayerFarming.Location == FollowerLocation.Base; }
            catch (Exception) { return true; }
        }

        /// <summary>
        /// Forget every remembered side of the threshold, so the next sample re-baselines
        /// silently. Called when a save is loaded: a cult that was already starving before
        /// the player quit has not just started starving now, and narrating the load would
        /// report four crossings that never happened.
        /// </summary>
        internal static void Rebaseline(string reason)
        {
            foreach (var state in States)
            {
                state.Known = false;
                state.Low = false;
                state.Locked = false;
                state.AnnouncedLow = false;
            }

            _baselined = false;
            Plugin.Log.LogInfo($"[cult status] rebaseline reason={reason}");
        }

        internal static void Shutdown() => Rebaseline("shutdown");

        private static void Evaluate(CultBar bar, BarState state, bool atBase)
        {
            if (!bar.Shown)
            {
                // A bar the player has not unlocked has no state worth remembering; revealing
                // it later should baseline afresh rather than immediately report a crossing.
                state.Known = false;
                return;
            }

            // The first sample of a bar records where it already was. Everything below
            // compares against that, so a scene load or a revealed bar establishes its side
            // of the line silently instead of narrating the state of the world.
            var first = !state.Known;
            state.Known = true;

            if (bar.Locked != state.Locked)
            {
                state.Locked = bar.Locked;
                if (!first && atBase)
                {
                    var text = CultStatusText.LockChanged(bar.Kind, bar.Locked);
                    Plugin.Log.LogInfo(
                        $"[cult status] lock bar={bar.Kind} locked={bar.Locked} " +
                        $"spoken=\"{text}\"");
                    Speaker.Say(text, SpeechPriority.Queued);
                }
            }

            // A frozen bar cannot move on its own, so a crossing while locked is not news and
            // would in any case be the lock's doing rather than the cult's. The side is still
            // recorded, so releasing the lock does not then read as a crossing.
            if (bar.Locked)
            {
                state.Low = bar.Normalised < CultStatusText.LowThreshold;
                return;
            }

            // Hysteresis on the way back up only. The faith drip and the waste count both
            // hover, and a bar resting exactly on a quarter would otherwise alternate between
            // low and recovered for as long as it sat there.
            var threshold = state.Low
                ? CultStatusText.LowThreshold + RecoveryMargin
                : CultStatusText.LowThreshold;
            var low = bar.Normalised < threshold;

            if (low == state.Low) return;

            state.Low = low;
            if (first) return;

            // A recovery nobody was warned about explains nothing and invites the player to
            // go looking for what they missed.
            if (!low && !state.AnnouncedLow)
            {
                Plugin.Log.LogInfo(
                    $"[cult status] crossing bar={bar.Kind} low=False " +
                    $"value={bar.Normalised:0.###} suppressed=unannounced-fall");
                return;
            }

            var crossing = CultStatusText.Crossing(bar.Kind, bar.Normalised, low);
            if (!atBase)
            {
                Plugin.Log.LogInfo(
                    $"[cult status] crossing bar={bar.Kind} low={low} " +
                    $"value={bar.Normalised:0.###} suppressed=not-at-base " +
                    $"would-say=\"{crossing}\"");
                return;
            }

            Plugin.Log.LogInfo(
                $"[cult status] crossing bar={bar.Kind} low={low} " +
                $"value={bar.Normalised:0.###} spoken=\"{crossing}\"");
            state.AnnouncedLow = low;
            Speaker.Say(crossing, low ? SpeechPriority.Now : SpeechPriority.Queued);
        }

        /// <summary>
        /// One structured line carrying every bar at once. Written on the baseline sample and
        /// on every requested readout, so a session log can answer "what were the bars doing
        /// when this happened?" without a crossing having to have occurred.
        /// </summary>
        private static void Log(string reason, CultStatusSnapshot snapshot, string spoken)
        {
            var message =
                $"[cult status] reason={reason} " +
                $"faith={Field(snapshot.Faith)} food={Field(snapshot.Food)} " +
                $"cleanliness={Field(snapshot.Cleanliness)} warmth={Field(snapshot.Warmth)} " +
                $"followers={snapshot.Followers} dead={snapshot.Dead}";

            if (spoken.Length > 0) message += $" spoken=\"{spoken}\"";
            Plugin.Log.LogInfo(message);
        }

        private static string Field(CultBar bar) =>
            !bar.Shown ? "hidden"
            : bar.Locked ? $"{CultStatusText.Percent(bar.Normalised)}%locked"
            : $"{CultStatusText.Percent(bar.Normalised)}%";
    }
}
