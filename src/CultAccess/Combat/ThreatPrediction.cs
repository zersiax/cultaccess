using System;

namespace CultAccess.Combat
{
    /// <summary>
    /// Pure relative-motion calculation used by the projectile warning layer. Keeping this
    /// independent of Unity makes the hit/miss boundary testable without launching the game.
    /// </summary>
    public static class ThreatPrediction
    {
        private const float StationarySpeedSquared = 0.000001f;

        public static bool TryPredict(
            float playerX,
            float playerY,
            float playerVelocityX,
            float playerVelocityY,
            float threatX,
            float threatY,
            float threatVelocityX,
            float threatVelocityY,
            float combinedRadius,
            float horizonSeconds,
            out float timeToClosest,
            out float closestDistance)
        {
            timeToClosest = 0f;
            closestDistance = float.PositiveInfinity;

            if (combinedRadius < 0f || horizonSeconds <= 0f) return false;

            var relativeX = threatX - playerX;
            var relativeY = threatY - playerY;
            var relativeVelocityX = threatVelocityX - playerVelocityX;
            var relativeVelocityY = threatVelocityY - playerVelocityY;
            var speedSquared = relativeVelocityX * relativeVelocityX +
                               relativeVelocityY * relativeVelocityY;

            if (speedSquared <= StationarySpeedSquared) return false;

            timeToClosest = -(relativeX * relativeVelocityX +
                              relativeY * relativeVelocityY) / speedSquared;
            if (timeToClosest < 0f || timeToClosest > horizonSeconds) return false;

            var closestX = relativeX + relativeVelocityX * timeToClosest;
            var closestY = relativeY + relativeVelocityY * timeToClosest;
            closestDistance = (float)Math.Sqrt(closestX * closestX + closestY * closestY);
            return closestDistance <= combinedRadius;
        }
    }
}
