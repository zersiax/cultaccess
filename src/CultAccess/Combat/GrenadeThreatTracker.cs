using System.Collections.Generic;
using CultAccess.Audio;
using UnityEngine;

namespace CultAccess.Combat
{
    /// <summary>Adapts visual grenade landing areas into the shared threat candidate model.</summary>
    internal static class GrenadeThreatTracker
    {
        private sealed class Track
        {
            internal GrenadeBullet Grenade;
            internal Vector3 Target;
            internal float Radius;
            internal float ImpactAtGameTime;
        }

        private static readonly Dictionary<int, Track> Grenades =
            new Dictionary<int, Track>();
        private static readonly List<int> StaleIds = new List<int>();

        internal static void Register(
            GrenadeBullet grenade,
            Health.Team team,
            Vector3 target,
            float timeToTravel)
        {
            if (grenade == null || team == Health.Team.PlayerTeam) return;

            var radius = ColliderGeometry.LargestRadius(grenade.damageColliderEvents);
            if (radius <= 0f)
            {
                Plugin.Log.LogWarning(
                    $"[combat grenade] no damage-collider radius; source={grenade.gameObject.name}");
                return;
            }

            var id = grenade.GetInstanceID();
            Grenades[id] = new Track
            {
                Grenade = grenade,
                Target = target,
                Radius = radius,
                ImpactAtGameTime = Time.time + Mathf.Max(0f, timeToTravel),
            };

            CombatDiagnostics.Info(
                $"[combat grenade] registered id={id} source={grenade.gameObject.name} " +
                $"target=({target.x:0.00},{target.y:0.00}) radius={radius:0.00} " +
                $"travel={timeToTravel:0.000}");
        }

        internal static void Impact(GrenadeBullet grenade, Vector3 target)
        {
            if (grenade == null) return;

            var id = grenade.GetInstanceID();
            if (!Grenades.TryGetValue(id, out var track)) return;

            CombatDiagnostics.Info(
                $"[combat grenade] impact id={id} source={grenade.gameObject.name} " +
                $"predictionError={Time.time - track.ImpactAtGameTime:0.000} " +
                $"targetError={Vector2.Distance(target, track.Target):0.00}");
            Grenades.Remove(id);
        }

        internal static void Unregister(GrenadeBullet grenade)
        {
            if (grenade != null) Grenades.Remove(grenade.GetInstanceID());
        }

        internal static void FindSoonest(
            PlayerFarming player,
            float playerRadius,
            Vector2 playerVelocity,
            float timeScale,
            float warningHorizon,
            ref CombatThreatCandidate best)
        {
            StaleIds.Clear();
            foreach (var pair in Grenades)
            {
                var track = pair.Value;
                if (track.Grenade == null || !track.Grenade.gameObject.activeInHierarchy)
                {
                    StaleIds.Add(pair.Key);
                    continue;
                }

                var remainingGameTime = track.ImpactAtGameTime - Time.time;
                var timeToImpact = Mathf.Max(0f, remainingGameTime / timeScale);
                if (timeToImpact > warningHorizon) continue;

                var predictedPlayer = (Vector2)player.transform.position +
                                      playerVelocity * timeToImpact;
                var combinedRadius = playerRadius + track.Radius;
                var closestDistance = Vector2.Distance(predictedPlayer, track.Target);
                if (closestDistance > combinedRadius) continue;

                var candidate = new CombatThreatCandidate
                {
                    Valid = true,
                    Source = track.Grenade.gameObject,
                    CuePosition = track.Target,
                    CueKind = CueId.AreaThreat,
                    Kind = "grenade",
                    TimeToImpact = timeToImpact,
                    ClosestDistance = closestDistance,
                    CombinedRadius = combinedRadius,
                    CurrentDistance = Vector2.Distance(player.transform.position, track.Target),
                };
                CombatThreatCandidate.ChooseSoonest(candidate, ref best);
            }

            foreach (var id in StaleIds)
                Grenades.Remove(id);
        }

        internal static void Reset()
        {
            Grenades.Clear();
            StaleIds.Clear();
        }
    }
}
