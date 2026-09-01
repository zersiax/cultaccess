using System.Collections.Generic;
using UnityEngine;

namespace CultAccess.Audio
{
    /// <summary>
    /// Finds what the always-on layer should be sounding, nearest first.
    ///
    /// Everything here comes from a registry the game maintains itself — <c>Health</c>'s team
    /// lists, <c>Projectile.Projectiles</c>, <c>PickUp.PickUps</c>, <c>Interaction.interactions</c>,
    /// <c>Follower.Followers</c>. That is not a preference: this runs several times a second
    /// forever, and a hierarchy-wide <c>FindObjectsOfType</c> at that rate is exactly the kind
    /// of cost the mod is not allowed to add. The registries are also incapable of going stale,
    /// because the game updates them as objects enable and disable.
    ///
    /// This is why the always-on layer does not reuse <c>WorldScanner</c>, which is richer but
    /// deliberately on-demand and does walk the hierarchy.
    /// </summary>
    internal static class SoundscapeSources
    {
        /// <summary>Reused so a per-frame gather allocates nothing.</summary>
        private static readonly List<Scored> Scratch = new List<Scored>(64);

        private struct Scored
        {
            internal Vector3 Position;
            internal float SquaredDistance;
        }

        /// <summary>
        /// Fill <paramref name="into"/> with up to <paramref name="max"/> positions of this
        /// category within <paramref name="radius"/>, nearest first.
        /// </summary>
        internal static void Collect(
            CueId cue,
            Vector3 origin,
            float radius,
            int max,
            List<Vector3> into)
        {
            into.Clear();
            Scratch.Clear();
            if (max <= 0 || radius <= 0f) return;

            var radiusSquared = radius * radius;

            switch (cue)
            {
                case CueId.AmbientEnemy:
                    AddTeam(Health.team2, origin, radiusSquared);
                    AddTeam(Health.dangerousAnimals, origin, radiusSquared);
                    break;
                case CueId.AmbientProjectile:
                    AddHostileProjectiles(origin, radiusSquared);
                    break;
                case CueId.AmbientItem:
                    AddPickups(origin, radiusSquared);
                    break;
                case CueId.AmbientInteractable:
                    AddInteractions(origin, radiusSquared, wantPickups: false);
                    break;
                case CueId.AmbientNpc:
                    AddFollowers(origin, radiusSquared);
                    break;
                default:
                    return;
            }

            Scratch.Sort((a, b) => a.SquaredDistance.CompareTo(b.SquaredDistance));

            var count = Scratch.Count < max ? Scratch.Count : max;
            for (var i = 0; i < count; i++)
                into.Add(Scratch[i].Position);
        }

        private static void AddTeam(List<Health> team, Vector3 origin, float radiusSquared)
        {
            if (team == null) return;

            foreach (var health in team)
            {
                if (health == null || !health.gameObject.activeInHierarchy) continue;

                // A corpse that has not despawned is still in the list. Announcing it as a
                // live threat is worse than staying quiet about it.
                if (!Alive(health)) continue;

                Consider(health.transform.position, origin, radiusSquared);
            }
        }

        private static bool Alive(Health health)
        {
            try
            {
                return health.CurrentHP > 0f;
            }
            catch
            {
                // Some units compute HP from state that may not be ready yet; treat an
                // unreadable unit as present rather than silently dropping a real threat.
                return true;
            }
        }

        /// <summary>
        /// The same hostility test the projectile warning uses, minus the trajectory
        /// prediction: this layer reports where hostile fire *is*, not where it will land.
        /// The two together are what makes a bullet-hell pattern legible.
        /// </summary>
        private static void AddHostileProjectiles(Vector3 origin, float radiusSquared)
        {
            var projectiles = Projectile.Projectiles;
            if (projectiles == null) return;

            foreach (var projectile in projectiles)
            {
                if (projectile == null || projectile.IsProjectilesParent) continue;
                if (!projectile.gameObject.activeInHierarchy) continue;
                if (projectile.team == Health.Team.PlayerTeam) continue;
                if (projectile.Damage <= 0f) continue;

                Consider(projectile.transform.position, origin, radiusSquared);
            }
        }

        private static void AddPickups(Vector3 origin, float radiusSquared)
        {
            var pickups = PickUp.PickUps;
            if (pickups != null)
            {
                foreach (var pickup in pickups)
                {
                    if (pickup == null || !pickup.gameObject.activeInHierarchy) continue;
                    Consider(pickup.transform.position, origin, radiusSquared);
                }
            }

            // Hearts are interactions rather than PickUps, and they are the pickup that
            // matters most in a fight, so they belong in the same category.
            AddInteractions(origin, radiusSquared, wantPickups: true);
        }

        private static void AddInteractions(Vector3 origin, float radiusSquared, bool wantPickups)
        {
            var interactions = Interaction.interactions;
            if (interactions == null) return;

            foreach (var interaction in interactions)
            {
                if (interaction == null || !interaction.isActiveAndEnabled) continue;

                var isPickup = interaction is Interaction_HeartPickupBase ||
                               interaction is Interaction_PickUpLoot;
                if (isPickup != wantPickups) continue;

                Consider(
                    interaction.transform.position + interaction.ActivatorOffset,
                    origin,
                    radiusSquared);
            }
        }

        private static void AddFollowers(Vector3 origin, float radiusSquared)
        {
            var followers = Follower.Followers;
            if (followers == null) return;

            foreach (var follower in followers)
            {
                if (follower == null || !follower.gameObject.activeInHierarchy) continue;
                Consider(follower.transform.position, origin, radiusSquared);
            }
        }

        /// <summary>
        /// Range is measured across the ground, not through the air.
        ///
        /// Z is height here, so a full 3D distance charges an airborne source for being in
        /// the air: the diving miniboss reaches about 2.5 units of arc height, which against
        /// a ten-unit enemy radius is a quarter of the range spent on altitude. The cue
        /// dimmed and could drop below the audible floor part-way through a dive, for an
        /// enemy that was directly overhead and about to land on the player.
        ///
        /// Planar also matches how every other distance the player hears is measured — the
        /// spoken "4 metres" and the beacon's ping rate are both planar.
        /// </summary>
        private static void Consider(Vector3 position, Vector3 origin, float radiusSquared)
        {
            var dx = position.x - origin.x;
            var dy = position.y - origin.y;
            var squared = dx * dx + dy * dy;
            if (squared > radiusSquared) return;

            Scratch.Add(new Scored { Position = position, SquaredDistance = squared });
        }
    }
}
