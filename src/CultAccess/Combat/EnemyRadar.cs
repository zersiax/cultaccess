using System.Collections.Generic;
using CultAccess.Audio;
using CultAccess.Navigation;
using CultAccess.Speech;
using CultAccess.Util;
using UnityEngine;

// UnityEngine.Compass is the device magnetometer; ours is the bearing helper.
using Compass = CultAccess.Navigation.Compass;

namespace CultAccess.Combat
{
    /// <summary>
    /// Enumerates hostiles and points the beacon at one.
    ///
    /// Built on <c>Health</c>'s static team lists, which the game maintains itself as units
    /// spawn, change team and die. That makes the enemy set free to read — no per-frame
    /// <c>FindObjectsOfType</c>, and never stale.
    ///
    /// Reads live values every time rather than caching HP or positions: a confidently
    /// announced stale health figure is worse than a slow one, and in combat everything
    /// moves.
    /// </summary>
    public static class EnemyRadar
    {
        private static readonly List<Health> Hostiles = new List<Health>();
        private static int _lockIndex = -1;

        /// <summary>Ignore hostiles beyond this distance in readouts.</summary>
        public static float Range = 30f;

        /// <summary>Speak a roster of nearby hostiles, nearest first.</summary>
        public static void AnnounceHostiles()
        {
            var player = NavigatorPlayer.Resolve();
            if (player == null)
            {
                Speaker.Say("Cannot find the player character.");
                return;
            }

            Collect(player.position);

            if (Hostiles.Count == 0)
            {
                Speaker.Say("No enemies nearby.");
                return;
            }

            var parts = new List<string>(Hostiles.Count);
            foreach (var hostile in Hostiles)
                parts.Add(Describe(hostile, player.position));

            var count = Hostiles.Count == 1 ? "1 enemy" : $"{Hostiles.Count} enemies";
            Speaker.Say($"{count}. {string.Join(". ", parts.ToArray())}");
        }

        /// <summary>
        /// Point the beacon at the next hostile, cycling nearest-first. Pressing past the
        /// last one turns the beacon off, so there is always a way back to silence.
        /// </summary>
        /// <summary>
        /// Step the enemy lock. Direction exists because the controller layer puts this on a
        /// D-pad, where left and right are one gesture apart and a forward-only cycle through
        /// five enemies to reach the one behind you is the sort of thing that stops a combat
        /// feature being used at all.
        ///
        /// Either direction still passes through "off" once per lap, so the beacon can always
        /// be dismissed without knowing how many enemies are left.
        /// </summary>
        public static void CycleBeaconTarget(int direction = 1)
        {
            var player = NavigatorPlayer.Resolve();
            if (player == null) return;

            Collect(player.position);

            if (Hostiles.Count == 0)
            {
                Beacon.ClearEnemy();
                _lockIndex = -1;
                Speaker.Say("No enemies nearby.");
                return;
            }

            _lockIndex += direction < 0 ? -1 : 1;
            if (_lockIndex >= Hostiles.Count || _lockIndex < -1)
            {
                _lockIndex = _lockIndex < -1 ? Hostiles.Count - 1 : -1;
                if (_lockIndex < 0)
                {
                    Beacon.ClearEnemy();
                    Speaker.Say("Enemy beacon off.");
                    return;
                }
            }

            var target = Hostiles[_lockIndex];
            Beacon.SetEnemyTarget(target.transform);
            CombatDiagnostics.Info(
                $"[enemy beacon] state=tracking target={EnemyName(target)} " +
                $"id={target.GetInstanceID()} world=({target.transform.position.x:0.00}," +
                $"{target.transform.position.y:0.00},{target.transform.position.z:0.00})");
            Speaker.Say($"Tracking {Describe(target, player.position)}");
        }

        /// <summary>
        /// Point the beacon at one specific enemy, keeping the radar's own index in step so
        /// its tick still drops the lock when that enemy dies.
        ///
        /// Called when the target list lands on an enemy, which is what makes stepping the
        /// enemies filter and locking the beacon one gesture rather than two. False if the
        /// enemy is not in range, in which case the caller should leave the beacon alone
        /// rather than clear it — the player has not asked for anything to change.
        /// </summary>
        public static bool LockBeaconOn(UnityEngine.Transform enemy)
        {
            if (enemy == null) return false;

            var player = NavigatorPlayer.Resolve();
            if (player == null) return false;

            Collect(player.position);
            for (var i = 0; i < Hostiles.Count; i++)
            {
                if (Hostiles[i] == null || Hostiles[i].transform != enemy) continue;

                _lockIndex = i;
                Beacon.SetEnemyTarget(enemy);
                return true;
            }

            return false;
        }

        /// <summary>Drop the beacon if its target died or left, so it never pings at a corpse.</summary>
        public static void Tick()
        {
            if (_lockIndex < 0) return;
            if (_lockIndex >= Hostiles.Count) return;

            var target = Hostiles[_lockIndex];
            if (IsAlive(target)) return;

            Beacon.ClearEnemy();
            _lockIndex = -1;
            CombatDiagnostics.Info("[enemy beacon] state=target-down");
            Speaker.Say("Target down.");
        }

        private static void Collect(Vector3 origin)
        {
            Hostiles.Clear();
            var rangeSquared = Range * Range;

            // team2 is the hostile team; dangerous animals are separately tracked but are
            // equally capable of killing the player, so they belong in the same readout.
            Gather(Health.team2, origin, rangeSquared);
            Gather(Health.dangerousAnimals, origin, rangeSquared);

            // Spawners first, then by distance. Killing the spawner ends the fight and kills
            // its brood outright, so it is the one target worth reaching — and burying it
            // partway down a list you cycle one press at a time is the same as not having it.
            Hostiles.Sort((a, b) =>
            {
                var spawnerA = EnemySpawners.IsSpawner(a);
                if (spawnerA != EnemySpawners.IsSpawner(b)) return spawnerA ? -1 : 1;

                return (a.transform.position - origin).sqrMagnitude
                    .CompareTo((b.transform.position - origin).sqrMagnitude);
            });
        }

        private static void Gather(List<Health> source, Vector3 origin, float rangeSquared)
        {
            if (source == null) return;

            foreach (var health in source)
            {
                if (!IsAlive(health)) continue;
                if (Hostiles.Contains(health)) continue;
                if ((health.transform.position - origin).sqrMagnitude > rangeSquared) continue;

                Hostiles.Add(health);
            }
        }

        private static bool IsAlive(Health health)
        {
            if (health == null) return false;
            if (!health.gameObject.activeInHierarchy) return false;

            try
            {
                return health.CurrentHP > 0f;
            }
            catch
            {
                // Some units compute HP from state that may not be ready.
                return true;
            }
        }

        private static string Describe(Health hostile, Vector3 origin)
        {
            var name = EnemyName(hostile);

            if (!Compass.TryDescribe(origin, hostile.transform.position, out var bearing, out var distance))
                return $"{name}, {Compass.DescribeDistance(distance)}";

            return $"{name}, {Compass.DescribeDistance(distance)}, {bearing}";
        }

        /// <summary>
        /// Enemies carry no player-facing name anywhere, so derive one from the type of the
        /// unit component — "EnemyBellKnight" becomes "Bell Knight". Better a mechanical name
        /// than an unidentifiable threat.
        /// </summary>
        private static string EnemyName(Health hostile)
        {
            var unit = hostile.GetComponent<UnitObject>() ?? hostile.GetComponentInParent<UnitObject>();
            var typeName = unit != null ? unit.GetType().Name : hostile.gameObject.name;

            if (typeName.StartsWith("Enemy", System.StringComparison.Ordinal) && typeName.Length > 5)
                typeName = typeName.Substring(5);

            var humanised = RichText.Humanise(typeName.Replace("(Clone)", string.Empty).Trim());
            if (humanised.Length == 0) humanised = "enemy";

            var marker = EnemySpawners.Marker(hostile);
            return marker == null ? humanised : $"{humanised}, {marker}";
        }
    }
}
