using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace CultAccess.Diagnostics
{
    /// <summary>
    /// Measures how long the mod's own per-frame work takes, so "the game felt laggy" can be
    /// turned into a number.
    ///
    /// Log-only, never spoken.
    ///
    /// Written because a report of lag during weapon attacks had no evidence behind it either
    /// way. The obvious suspect — the damage patch building diagnostic strings on every hit —
    /// turned out to be innocent: it returns early unless the damage is to the player, and the
    /// session logged twenty-one such events. Guessing again from there would have been
    /// picking the next most plausible cause and presenting it as the answer, which is how a
    /// tester ends up chasing the wrong thing for a session.
    ///
    /// What this can and cannot see is worth stating. It measures the work the mod does from
    /// its own update, which is most of it. It does **not** measure the Harmony patch handlers,
    /// which run inside the game's call stack rather than ours. If these numbers stay small
    /// while the game still stutters, that is itself informative: it moves the suspicion to
    /// the patches, or off the mod entirely.
    /// </summary>
    internal static class FrameBudget
    {
        /// <summary>
        /// Only report when something took long enough to notice. A frame at sixty per second
        /// has 16.7 ms for everything, and an accessibility mod has no business taking a
        /// noticeable share of that — the less so on a machine that is already short of
        /// cores, where the mod's slice costs proportionally more than the number suggests.
        /// </summary>
        private const double ReportThresholdMs = 2.0;

        private const float WindowSeconds = 10f;

        private sealed class Scope
        {
            internal double TotalMs;
            internal double WorstMs;
            internal int Samples;
        }

        private static readonly Dictionary<string, Scope> Scopes =
            new Dictionary<string, Scope>();

        private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        private static float _windowEndsAt;
        private static bool _reportedBaseline;

        internal static bool Enabled = true;

        /// <summary>Timestamp to pass back to <see cref="End"/>. Cheap enough to call anywhere.</summary>
        internal static long Begin() => Enabled ? Stopwatch.GetTimestamp() : 0L;

        internal static void End(string scope, long started)
        {
            if (!Enabled || started == 0L) return;

            var elapsed = (Stopwatch.GetTimestamp() - started) * TicksToMs;

            if (!Scopes.TryGetValue(scope, out var record))
                Scopes[scope] = record = new Scope();

            record.TotalMs += elapsed;
            record.Samples++;
            if (elapsed > record.WorstMs) record.WorstMs = elapsed;
        }

        /// <summary>
        /// Report once per window when something exceeded the threshold, plus a baseline the
        /// first time regardless.
        ///
        /// The baseline is not padding. Reporting only on exceedance means a healthy session
        /// produces no output at all, which is indistinguishable from the measurement never
        /// having run — the exact failure this project treats as unacceptable everywhere
        /// else, and it would be embarrassing to build it into the instrument meant to catch
        /// it. One line early says what normal looks like on this machine, and gives every
        /// later report something to be compared against.
        /// </summary>
        internal static void Tick()
        {
            if (!Enabled) return;

            if (Time.unscaledTime < _windowEndsAt)
            {
                if (_windowEndsAt > 0f) return;
            }

            _windowEndsAt = Time.unscaledTime + WindowSeconds;

            var worst = 0.0;
            foreach (var record in Scopes.Values)
                if (record.WorstMs > worst)
                    worst = record.WorstMs;

            var onlyBaseline = !_reportedBaseline;
            if (worst >= ReportThresholdMs || onlyBaseline)
            {
                if (!_reportedBaseline)
                {
                    _reportedBaseline = true;
                    Plugin.Log.LogInfo(
                        "[frame budget] baseline for this machine, reported once; later lines " +
                        $"appear only when something exceeds {ReportThresholdMs:0.0}ms");
                }

                foreach (var pair in Scopes)
                {
                    var record = pair.Value;
                    if (record.Samples == 0) continue;

                    // Print the total always, and a named part only when it is itself worth
                    // looking at. With a scope per subsystem, printing every one would bury
                    // the line that matters under a dozen that read 0.00.
                    if (pair.Key != "update" && record.WorstMs < 1.0 && !onlyBaseline) continue;

                    Plugin.Log.LogInfo(
                        $"[frame budget] scope={pair.Key} worst={record.WorstMs:0.00}ms " +
                        $"average={record.TotalMs / record.Samples:0.000}ms " +
                        $"frames={record.Samples}");
                }
            }

            Scopes.Clear();
        }
    }
}
