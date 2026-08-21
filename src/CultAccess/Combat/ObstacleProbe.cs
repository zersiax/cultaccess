using UnityEngine;

namespace CultAccess.Combat
{
    /// <summary>
    /// Finds the nearest solid collider along a direction, using the same collision rules the
    /// game itself would apply to the player moving that way.
    ///
    /// Shared by the two systems that need it and answer different questions with it: the
    /// movement-corridor warning ("will continuing this way hit something?") and the always-on
    /// wall sonar ("what shape is the space I am standing in?"). One implementation, because a
    /// second one would inevitably disagree with the first about what counts as solid, and the
    /// player would hear two different worlds.
    /// </summary>
    internal static class ObstacleProbe
    {
        private static readonly RaycastHit2D[] CastHits = new RaycastHit2D[32];

        /// <summary>
        /// Sweep the player's own collider along <paramref name="direction"/> and return the
        /// nearest thing it would actually collide with.
        ///
        /// The layer mask comes from <c>Physics2D.GetLayerCollisionMask</c> for the player's
        /// own layer, and ignored collision pairs are honoured, so this cannot report a wall
        /// the player can walk straight through.
        /// </summary>
        internal static bool TryFind(
            PlayerFarming player,
            CircleCollider2D playerCollider,
            Vector2 direction,
            float distance,
            out RaycastHit2D nearest)
        {
            nearest = default;
            if (player == null || playerCollider == null || distance <= 0f ||
                direction.sqrMagnitude <= 0f)
                return false;

            var origin = (Vector2)playerCollider.bounds.center;
            var radius = ColliderGeometry.Radius(playerCollider);
            var layerMask = Physics2D.GetLayerCollisionMask(player.gameObject.layer);

            int count;
            try
            {
                count = Physics2D.CircleCastNonAlloc(
                    origin, radius, direction, CastHits, distance, layerMask);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[obstacle probe] circle cast failed: {e}");
                return false;
            }

            var found = false;
            var nearestDistance = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var candidate = CastHits[i];
                var other = candidate.collider;
                if (other == null || !other.enabled || other.isTrigger) continue;
                if (other.GetComponentInParent<PlayerFarming>() == player) continue;
                if (Physics2D.GetIgnoreCollision(playerCollider, other)) continue;
                if (candidate.distance >= nearestDistance) continue;

                found = true;
                nearestDistance = candidate.distance;
                nearest = candidate;
            }

            return found;
        }
    }
}
