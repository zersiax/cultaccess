using System.Collections.Generic;
using CultAccess.Util;
using Spine.Unity;
using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Finds nearby things worth walking to, from several sources.
    ///
    /// Dungeon room exits are <c>Door</c> triggers, not <c>Interaction</c>s: walking into an
    /// open one changes rooms without an input. Other portals can still be interactions.
    /// <c>Interaction</c> does <em>not</em> cover characters standing around, which is why
    /// followers and other Spine-driven characters are scanned separately: a character who
    /// gates progression is useless to a player who cannot find them.
    ///
    /// Scanning is on demand only — a hierarchy-wide component search is far too expensive
    /// to run per frame.
    /// </summary>
    public static class WorldScanner
    {
        /// <summary>Ignore things further away than this when scanning.</summary>
        public static float ScanRadius = 40f;

        /// <summary>Log characters we could find but could not name. See Plugin.LogScanCandidates.</summary>
        public static bool LogUnnamedCharacters;

        public static List<PointOfInterest> Scan(Vector3 origin)
        {
            var found = new List<PointOfInterest>();
            var radiusSquared = ScanRadius * ScanRadius;
            var player = NavigatorPlayer.Resolve();

            InteractionScanner.Add(origin, radiusSquared, found);
            PendingRecruitTarget.Add(origin, radiusSquared, found);
            AddFollowers(origin, radiusSquared, found);
            AddEnemies(origin, radiusSquared, found);
            AddLooseCharacters(origin, radiusSquared, player, found);

            found.Sort((a, b) =>
                (a.AimPosition - origin).sqrMagnitude.CompareTo((b.AimPosition - origin).sqrMagnitude));

            return found;
        }

        private static void AddFollowers(Vector3 origin, float radiusSquared, List<PointOfInterest> found)
        {
            foreach (var follower in Object.FindObjectsOfType<Follower>())
            {
                if (follower == null) continue;

                var transform = follower.transform;
                if ((transform.position - origin).sqrMagnitude > radiusSquared) continue;

                // A follower with its own interaction prompt is already listed; listing the
                // same character twice makes the cycle list confusing to step through.
                if (AlreadyCovered(transform, found)) continue;

                var name = FollowerName(follower) ?? "Follower";

                found.Add(new PointOfInterest
                {
                    Transform = transform,
                    Name = name,
                    Kind = PoiKind.Follower,
                });
            }
        }

        /// <summary>Hostiles, so combat rooms are navigable rather than empty.</summary>
        private static void AddEnemies(Vector3 origin, float radiusSquared, List<PointOfInterest> found)
        {
            AddTeam(Health.team2, origin, radiusSquared, found);
            AddTeam(Health.dangerousAnimals, origin, radiusSquared, found);
        }

        private static void AddTeam(List<Health> team, Vector3 origin, float radiusSquared, List<PointOfInterest> found)
        {
            if (team == null) return;

            foreach (var health in team)
            {
                if (health == null || !health.gameObject.activeInHierarchy) continue;

                var transform = health.transform;
                if ((transform.position - origin).sqrMagnitude > radiusSquared) continue;
                if (AlreadyCovered(transform, found)) continue;

                found.Add(new PointOfInterest
                {
                    Transform = transform,
                    Name = EnemyName(health),
                    Kind = PoiKind.Enemy,
                });
            }
        }

        private static string EnemyName(Health health)
        {
            var unit = health.GetComponent<UnitObject>() ?? health.GetComponentInParent<UnitObject>();
            var typeName = unit != null ? unit.GetType().Name : health.gameObject.name;

            if (typeName.StartsWith("Enemy", System.StringComparison.Ordinal) && typeName.Length > 5)
                typeName = typeName.Substring(5);

            var humanised = RichText.Humanise(typeName.Replace("(Clone)", string.Empty).Trim());
            return humanised.Length == 0 ? "enemy" : humanised;
        }

        /// <summary>
        /// Spine-animated characters that are neither interactable, follower nor enemy: story
        /// NPCs, cultists in scripted scenes. They have no label anywhere, so they are named
        /// from their object name as a last resort.
        ///
        /// Requires a <c>Health</c> or <c>StateMachine</c> component to qualify. Spine rigs
        /// are used for scenery and effects too, and without this filter the list fills up
        /// with torch flames, warning icons and other props — which is what made it unwieldy.
        /// Having health or a state machine is what actually distinguishes a character from
        /// an animated decoration.
        /// </summary>
        private static void AddLooseCharacters(
            Vector3 origin,
            float radiusSquared,
            Transform player,
            List<PointOfInterest> found)
        {
            foreach (var skeleton in Object.FindObjectsOfType<SkeletonAnimation>())
            {
                if (skeleton == null) continue;

                var transform = skeleton.transform;
                if ((transform.position - origin).sqrMagnitude > radiusSquared) continue;
                if (SameHierarchy(transform, player)) continue;
                if (!IsCharacter(transform)) continue;
                if (AlreadyCovered(transform, found)) continue;

                var name = CharacterName(transform);
                if (name == null) continue;

                if (LogUnnamedCharacters)
                    Plugin.Log.LogInfo($"[scan] unnamed character \"{transform.name}\" " +
                                       $"components: {ComponentList(transform)}");

                found.Add(new PointOfInterest
                {
                    Transform = transform,
                    Name = name,
                    Kind = PoiKind.Character,
                });
            }
        }

        /// <summary>
        /// The Lamb itself is a Spine character with health and a state machine, so it
        /// satisfies every loose-character test. Never offer the player's own rig as a
        /// navigation destination.
        /// </summary>
        private static bool SameHierarchy(Transform candidate, Transform player)
        {
            if (candidate == null || player == null) return false;
            return candidate == player || candidate.IsChildOf(player) || player.IsChildOf(candidate);
        }

        /// <summary>
        /// A Spine rig is a character only if it has health or a state machine. Scenery
        /// (torch flames, warning icons) is animated the same way but has neither.
        /// </summary>
        private static bool IsCharacter(Transform transform)
        {
            if (transform.GetComponentInParent<Health>() != null) return true;
            if (transform.GetComponentInParent<StateMachine>() != null) return true;
            return false;
        }

        /// <summary>True if something in the same object hierarchy is already listed.</summary>
        private static bool AlreadyCovered(Transform candidate, List<PointOfInterest> found)
        {
            foreach (var poi in found)
            {
                var existing = poi.Transform;
                if (existing == null) continue;
                if (existing == candidate) return true;
                if (candidate.IsChildOf(existing) || existing.IsChildOf(candidate)) return true;
            }

            return false;
        }

        private static string FollowerName(Follower follower)
        {
            try
            {
                var name = follower.Brain?.Info?.Name;
                var clean = RichText.Clean(name);
                return clean.Length == 0 ? null : clean;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Object names are developer-facing, so drop the obvious noise before speaking one.
        /// Anything that still looks like a prop rather than a character is dropped entirely.
        /// </summary>
        internal static string CharacterName(Transform transform)
        {
            var raw = transform.name;
            if (string.IsNullOrEmpty(raw)) return null;

            // Unity clone suffixes and Spine rig suffixes carry no meaning for the player.
            var trimmed = raw.Replace("(Clone)", string.Empty).Trim();
            if (trimmed.EndsWith("_Spine", System.StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - "_Spine".Length);
            if (trimmed.Equals("Spine", System.StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("Skeleton", System.StringComparison.OrdinalIgnoreCase))
            {
                // Unhelpful generic rig name: try the parent, which is usually the character.
                trimmed = transform.parent == null ? null : transform.parent.name;
                if (string.IsNullOrEmpty(trimmed)) return null;
                trimmed = trimmed.Replace("(Clone)", string.Empty).Trim();
            }

            var humanised = RichText.Humanise(trimmed);
            return humanised.Length == 0 ? null : humanised;
        }

        private static string ComponentList(Transform transform)
        {
            var names = new List<string>();
            foreach (var component in transform.GetComponents<Component>())
            {
                if (component == null) continue;
                names.Add(component.GetType().Name);
            }

            return string.Join(", ", names.ToArray());
        }
    }
}
