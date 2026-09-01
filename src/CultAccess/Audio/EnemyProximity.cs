using System.Collections.Generic;
using CultAccess.Navigation;
using UnityEngine;

namespace CultAccess.Audio
{
    /// <summary>
    /// The always-on enemy cue, with distance carried by repeat rate rather than by volume
    /// alone.
    ///
    /// Every other ambient category pings its whole set together on one fixed interval, and
    /// distance shows up only as loudness through the falloff curve. For scenery that is
    /// right. For an enemy it is the wrong channel: the player's own report was that the cue
    /// tells you where something is but not that you have just gone from four metres to two,
    /// and that it gets drowned out. Both are the same defect.
    ///
    /// Level cannot fix it. `AmbientEnemyVolume` was already at its maximum of 1.0 when that
    /// was reported, against event cues reaching 1.0 and a measured `AmbientEnemy` peak of
    /// 0.19 to 0.66. Rate can: a changing rhythm survives masking far better than an absolute
    /// level does, and it is a channel the player already reads fluently, because the
    /// navigation beacon has always encoded distance the same way.
    ///
    /// So each hostile gets its own schedule rather than sharing the category's. Enemies at
    /// different distances desynchronise, which is the point — being surrounded should sound
    /// like several rhythms at once, with the fast one telling you which way to worry.
    ///
    /// Identity is free here and nowhere else: `Health.team2` is a registry the game itself
    /// maintains, so a hostile can be keyed by instance id across frames. That is what makes
    /// per-source timing affordable, and why this category gets its own path while the rest
    /// stay on the shared one.
    /// </summary>
    internal static class EnemyProximity
    {
        /// <summary>
        /// Fraction of the configured repeat used at point-blank range. The configured value
        /// stays meaningful as the rate at the edge of the radius, so a player who has already
        /// tuned `AmbientEnemyRepeat` keeps what they chose and gains a floor beneath it.
        /// </summary>
        private const float ClosestIntervalFactor = 0.3f;

        /// <summary>
        /// Never faster than this regardless of configuration. Several hostiles at arm's
        /// length would otherwise merge into a buzz, which carries no distance at all and
        /// spends the whole per-second budget doing it.
        /// </summary>
        private const float FastestInterval = 0.18f;

        private static readonly Dictionary<int, float> NextPingAt = new Dictionary<int, float>();
        private static readonly List<Health> Nearby = new List<Health>();
        private static readonly List<int> Expired = new List<int>();

        internal static void Reset()
        {
            NextPingAt.Clear();
            Nearby.Clear();
        }

        /// <summary>
        /// Play whichever hostiles are due. Returns how many cues were spent, so the caller
        /// keeps them inside the same per-second ambient budget as every other category.
        /// </summary>
        internal static int Tick(Vector3 origin, SoundscapeSettings settings, int budgetRemaining)
        {
            Collect(origin, settings.Radius, settings.MaxSources);
            Forget();

            if (Nearby.Count == 0 || budgetRemaining <= 0) return 0;

            var now = Time.unscaledTime;
            var spent = 0;

            foreach (var hostile in Nearby)
            {
                if (spent >= budgetRemaining) break;

                var id = hostile.GetInstanceID();
                if (NextPingAt.TryGetValue(id, out var due) && now < due) continue;

                var position = hostile.transform.position;
                var distance = RoutePlanarMath.Distance(
                    origin.x, origin.y, position.x, position.y);

                // Re-armed before the cue is attempted, so a source that fails to play cannot
                // be retried every frame for as long as it fails.
                NextPingAt[id] = now + Interval(distance, settings);

                var volume = SoundscapeFalloff.Volume(distance, settings.Radius, settings.Falloff);
                if (!SoundscapeFalloff.Audible(volume)) continue;

                if (CuePlayer.Play(CueId.AmbientEnemy, position, volume)) spent++;
            }

            return spent;
        }

        /// <summary>
        /// Interval for one hostile: the configured repeat at the edge of the radius, falling
        /// toward <see cref="FastestInterval"/> as it closes. Deliberately the same shape as
        /// the navigation beacon's, so the two speak the same language about distance.
        /// </summary>
        internal static float Interval(float distance, SoundscapeSettings settings)
        {
            var slowest = Mathf.Max(0.05f, settings.Interval);
            var fastest = Mathf.Max(FastestInterval, slowest * ClosestIntervalFactor);

            var closeness = settings.Radius <= 0f
                ? 0f
                : 1f - Mathf.Clamp01(distance / settings.Radius);

            return Mathf.Lerp(slowest, fastest, closeness);
        }

        private static void Collect(Vector3 origin, float radius, int max)
        {
            Nearby.Clear();
            if (max <= 0 || radius <= 0f) return;

            var radiusSquared = radius * radius;
            Gather(Health.team2, origin, radiusSquared);
            Gather(Health.dangerousAnimals, origin, radiusSquared);

            // Nearest first, so the budget is spent on what matters when it runs short.
            Nearby.Sort((a, b) =>
                Planar(a, origin).CompareTo(Planar(b, origin)));

            if (Nearby.Count > max) Nearby.RemoveRange(max, Nearby.Count - max);
        }

        private static void Gather(List<Health> source, Vector3 origin, float radiusSquared)
        {
            if (source == null) return;

            foreach (var health in source)
            {
                if (health == null || !health.gameObject.activeInHierarchy) continue;
                if (health.CurrentHP <= 0f) continue;
                if (Nearby.Contains(health)) continue;
                if (Planar(health, origin) > radiusSquared) continue;

                Nearby.Add(health);
            }
        }

        private static float Planar(Health health, Vector3 origin)
        {
            var position = health.transform.position;
            var dx = position.x - origin.x;
            var dy = position.y - origin.y;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Drop schedules for hostiles that have died or left, so a long session cannot
        /// accumulate an entry per enemy the player has ever met.
        /// </summary>
        private static void Forget()
        {
            if (NextPingAt.Count <= Nearby.Count) return;

            Expired.Clear();
            foreach (var pair in NextPingAt)
            {
                var live = false;
                foreach (var hostile in Nearby)
                {
                    if (hostile.GetInstanceID() != pair.Key) continue;

                    live = true;
                    break;
                }

                if (!live) Expired.Add(pair.Key);
            }

            foreach (var id in Expired) NextPingAt.Remove(id);
        }
    }
}
