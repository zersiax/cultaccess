using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CultAccess.Combat
{
    /// <summary>
    /// Which enemies are generating the others, and therefore which one to kill.
    ///
    /// A session log caught a fight whose enemy count went 2, 3, 4, 3, 2, 3, 2, 3, 2 — the
    /// player killing spawn as fast as it arrived. The source was an `EnemyWormTurret`, which
    /// keeps a list of what it has spawned and, on its own death, deals every one of them
    /// their full health in damage. Its brood is also marked `GiveXP = false`. So killing the
    /// turret ends the fight instantly and killing anything else is worth nothing.
    ///
    /// A sighted player sees the big worm and shoots it. We announced "Combat, 2 enemies" and
    /// then said nothing that distinguished the source from its output, which is the
    /// difference between a fight that ends and one that does not.
    ///
    /// The signal is the game's own structure rather than a list of type names to maintain:
    /// every spawner in this game declares a private field called `spawnedEnemies`. The
    /// element type varies — `Health`, `UnitObject`, `GameObject` — so the field *name* is
    /// what is matched, across the whole type hierarchy. Names are only a fallback, for
    /// spawners that hold their brood some other way.
    /// </summary>
    internal static class EnemySpawners
    {
        private const string SpawnedField = "spawnedEnemies";

        private const BindingFlags AnyDeclared =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        /// <summary>
        /// Cached per component type, which is why this is affordable at all: the reflection
        /// happens once per enemy species per session, and every later hostile of that species
        /// is a dictionary hit. Negative results are cached too — almost nothing is a spawner,
        /// so the miss is the common case and the one worth making free.
        /// </summary>
        private static readonly Dictionary<Type, bool> Known = new Dictionary<Type, bool>();

        internal static bool IsSpawner(Health health)
        {
            if (health == null) return false;

            var unit = health.GetComponent<UnitObject>() ?? health.GetComponentInParent<UnitObject>();
            var type = unit != null ? unit.GetType() : health.GetType();
            return IsSpawnerType(type);
        }

        internal static bool IsSpawner(Transform enemy)
        {
            if (enemy == null) return false;

            var unit = enemy.GetComponent<UnitObject>() ?? enemy.GetComponentInParent<UnitObject>();
            return unit != null && IsSpawnerType(unit.GetType());
        }

        /// <summary>The word appended to a spawner's name, or null for an ordinary enemy.</summary>
        internal static string Marker(Health health) => IsSpawner(health) ? "spawner" : null;

        private static bool IsSpawnerType(Type type)
        {
            if (type == null) return false;
            if (Known.TryGetValue(type, out var known)) return known;

            var result = false;
            try
            {
                for (var current = type; current != null; current = current.BaseType)
                {
                    if (current.GetField(SpawnedField, AnyDeclared) == null) continue;

                    result = true;
                    break;
                }

                if (!result) result = NameSuggestsSpawner(type.Name);

                if (result)
                    Plugin.Log.LogInfo($"[combat spawner] {type.Name} generates other enemies");
            }
            catch (Exception e)
            {
                // Never silently. Getting this wrong means the player is told to kill the
                // wrong thing, which is worse than not being told anything.
                Plugin.Log.LogWarning(
                    $"[combat spawner] could not inspect {type.Name}, treating as ordinary: {e.Message}");
            }

            Known[type] = result;
            return result;
        }

        private static bool NameSuggestsSpawner(string typeName) =>
            !string.IsNullOrEmpty(typeName) &&
            (typeName.IndexOf("Spawner", StringComparison.OrdinalIgnoreCase) >= 0 ||
             typeName.IndexOf("Nest", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
