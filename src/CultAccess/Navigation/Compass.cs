using UnityEngine;

namespace CultAccess.Navigation
{
    /// <summary>
    /// Turns a world-space offset into a direction a player can act on.
    ///
    /// Bearings are computed in <em>screen</em> space, not world space, and that is the
    /// important decision here. The camera is fixed-rotation and top-down, so the movement
    /// stick maps directly onto screen axes: "north east" means push up and right, with no
    /// mental rotation needed. Deriving the bearing from the camera projection rather than
    /// from world axes also means a camera that does rotate or tilt stays correct for free,
    /// and it sidesteps having to know whether the game's ground plane is XY or XZ.
    ///
    /// North is screen up. Distances stay in world units, since that is what predicts how
    /// long a walk actually takes.
    /// </summary>
    public static class Compass
    {
        private static readonly string[] Points =
        {
            "north", "north east", "east", "south east",
            "south", "south west", "west", "north west",
        };

        /// <summary>
        /// Describe the offset from <paramref name="from"/> to <paramref name="to"/>.
        /// Returns false when there is no camera to project through, or the two points are
        /// close enough together that a bearing would be meaningless noise.
        /// </summary>
        public static bool TryDescribe(Vector3 from, Vector3 to, out string bearing, out float distance)
        {
            bearing = null;
            distance = Vector3.Distance(from, to);

            var camera = Camera.main;
            if (camera == null) return false;

            var screenFrom = camera.WorldToScreenPoint(from);
            var screenTo = camera.WorldToScreenPoint(to);
            var delta = new Vector2(screenTo.x - screenFrom.x, screenTo.y - screenFrom.y);

            // A few pixels of separation carries no usable direction.
            if (delta.sqrMagnitude < 4f) return false;

            bearing = Points[OctantIndex(delta)];
            return true;
        }

        /// <summary>Compass point for a screen-space delta, where +y is north.</summary>
        public static string Describe(Vector2 screenDelta) => Points[OctantIndex(screenDelta)];

        private static int OctantIndex(Vector2 delta)
        {
            // Atan2(x, y) puts 0 degrees at screen up and grows clockwise, which matches
            // the order of Points.
            var degrees = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            if (degrees < 0f) degrees += 360f;

            // Offset by half a sector so each name covers the range centred on it.
            return (int)((degrees + 22.5f) / 45f) % 8;
        }

        /// <summary>Distance phrased for speech: coarse far away, precise up close.</summary>
        public static string DescribeDistance(float distance)
        {
            if (distance < 1.5f) return "right here";
            if (distance < 10f) return $"{distance:0.#} metres";
            return $"{Mathf.RoundToInt(distance)} metres";
        }

        /// <summary>
        /// A movement instruction always needs a measurable amount. "Go north for right
        /// here" is not actionable, even though "the target is right here" is useful.
        /// </summary>
        public static string DescribeTravelDistance(float distance)
        {
            if (distance < 10f) return $"{Mathf.Max(0f, distance):0.#} metres";
            return $"{Mathf.RoundToInt(distance)} metres";
        }
    }
}
