using System;

namespace CultAccess.Navigation
{
    /// <summary>Pure XY geometry for the game's two-dimensional walking graph.</summary>
    internal static class RoutePlanarMath
    {
        public static float Distance(float ax, float ay, float bx, float by)
        {
            var dx = bx - ax;
            var dy = by - ay;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
