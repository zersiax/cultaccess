using UnityEngine;

namespace CultAccess.Combat
{
    /// <summary>Reads live collider dimensions without enabling or moving the collider.</summary>
    internal static class ColliderGeometry
    {
        internal static float Radius(Collider2D collider)
        {
            if (collider == null) return 0f;

            var scale = collider.transform.lossyScale;
            var scaleX = Mathf.Abs(scale.x);
            var scaleY = Mathf.Abs(scale.y);

            if (collider is CircleCollider2D circle)
                return circle.radius * Mathf.Max(scaleX, scaleY);

            if (collider is BoxCollider2D box)
            {
                var halfWidth = box.size.x * scaleX * 0.5f;
                var halfHeight = box.size.y * scaleY * 0.5f;
                return Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);
            }

            if (collider is CapsuleCollider2D capsule)
            {
                var halfWidth = capsule.size.x * scaleX * 0.5f;
                var halfHeight = capsule.size.y * scaleY * 0.5f;
                return Mathf.Max(halfWidth, halfHeight);
            }

            if (collider is PolygonCollider2D polygon)
            {
                var largestSquared = 0f;
                for (var pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
                {
                    var path = polygon.GetPath(pathIndex);
                    foreach (var point in path)
                    {
                        var x = (point.x + polygon.offset.x) * scaleX;
                        var y = (point.y + polygon.offset.y) * scaleY;
                        largestSquared = Mathf.Max(largestSquared, x * x + y * y);
                    }
                }

                if (largestSquared > 0f) return Mathf.Sqrt(largestSquared);
            }

            // Bounds remain authoritative for enabled collider shapes not handled above.
            var extents = collider.bounds.extents;
            return Mathf.Sqrt(extents.x * extents.x + extents.y * extents.y);
        }

        internal static float LargestRadius(ColliderEvents events)
        {
            if (events == null) return 0f;

            var largest = 0f;
            var colliders = events.GetComponentsInChildren<Collider2D>(includeInactive: true);
            foreach (var collider in colliders)
                largest = Mathf.Max(largest, Radius(collider));

            return largest;
        }
    }
}
