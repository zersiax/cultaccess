using System;
using System.Collections.Generic;
using System.Globalization;

namespace CultAccess.Status
{
    /// <summary>
    /// A point-in-time copy of the heart values rendered by <c>HUD_Hearts</c>.
    /// The game stores two health points per displayed heart.
    /// </summary>
    public readonly struct HealthSnapshot
    {
        public HealthSnapshot(
            float red, float redCapacity, float spirit, float spiritCapacity,
            float blue, float black, float fire, float ice)
        {
            Red = red;
            RedCapacity = redCapacity;
            Spirit = spirit;
            SpiritCapacity = spiritCapacity;
            Blue = blue;
            Black = black;
            Fire = fire;
            Ice = ice;
        }

        public float Red { get; }
        public float RedCapacity { get; }
        public float Spirit { get; }
        public float SpiritCapacity { get; }
        public float Blue { get; }
        public float Black { get; }
        public float Fire { get; }
        public float Ice { get; }

        public float Current => Red + Spirit + Blue + Black + Fire + Ice;

        // Blue, black, fire, and ice hearts disappear when spent, just as their HUD
        // slots do. Their live amount is therefore also their live capacity.
        public float Capacity =>
            RedCapacity + SpiritCapacity + Blue + Black + Fire + Ice;
    }

    /// <summary>Pure wording policy for automatic and requested player-status reads.</summary>
    public static class PlayerStatusText
    {
        private const float HealthPointsPerHeart = 2f;
        private const float ComparisonTolerance = 0.001f;

        public static string HealthChange(HealthSnapshot before, HealthSnapshot after)
        {
            var currentDelta = after.Current - before.Current;
            if (currentDelta < -ComparisonTolerance)
            {
                if (after.Current <= ComparisonTolerance)
                    return "Health depleted";

                return $"Health dropped to {HealthValue(after.Current)} of " +
                       $"{HealthValue(after.Capacity)} hearts";
            }

            if (currentDelta > ComparisonTolerance)
            {
                var special = SpecialHeartGain(before, after);
                if (special.Length > 0)
                    return $"{special}. Health {HealthValue(after.Current)} of " +
                           $"{HealthValue(after.Capacity)} hearts";

                if (after.RedCapacity - before.RedCapacity > ComparisonTolerance)
                    return $"Maximum health increased to {HealthValue(after.RedCapacity)} hearts. " +
                           $"Health {HealthValue(after.Current)} of {HealthValue(after.Capacity)} hearts";

                return $"Health rose to {HealthValue(after.Current)} of " +
                       $"{HealthValue(after.Capacity)} hearts";
            }

            var capacityDelta = after.Capacity - before.Capacity;
            if (capacityDelta > ComparisonTolerance)
                return $"Maximum health increased to {HealthValue(after.Capacity)} hearts";
            if (capacityDelta < -ComparisonTolerance)
                return $"Maximum health dropped to {HealthValue(after.Capacity)} hearts";

            return string.Empty;
        }

        public static string HealthStatus(HealthSnapshot health)
        {
            var text = $"Health {HealthValue(health.Current)} of " +
                       $"{HealthValue(health.Capacity)} hearts";
            var parts = new List<string>
            {
                $"red {HealthValue(health.Red)} of {HealthValue(health.RedCapacity)}"
            };

            if (health.SpiritCapacity > ComparisonTolerance)
                parts.Add($"spirit {HealthValue(health.Spirit)} of " +
                          HealthValue(health.SpiritCapacity));
            AddSpecial(parts, "blue", health.Blue);
            AddSpecial(parts, "black", health.Black);
            AddSpecial(parts, "fire", health.Fire);
            AddSpecial(parts, "ice", health.Ice);

            return text + ". " + string.Join(", ", parts.ToArray());
        }

        public static string HealthPickup(
            string pickupName, HealthSnapshot before, HealthSnapshot after)
        {
            var change = HealthChange(before, after);
            if (change.Length == 0) return string.Empty;

            var name = string.IsNullOrWhiteSpace(pickupName)
                ? "Heart pickup"
                : pickupName.Trim();
            return $"Picked up {name}. {change}";
        }

        public static string FervourChange(
            float before, float current, float total, float curseCost)
        {
            if (Math.Abs(current - before) <= ComparisonTolerance) return string.Empty;

            if (current <= ComparisonTolerance)
                return "Fervour empty, no curses ready";

            if (total > ComparisonTolerance && current >= total - ComparisonTolerance)
                return $"Fervour full, {ReadyCurses(current, curseCost)}";

            var direction = current > before ? "rose" : "dropped";
            return $"Fervour {direction} to {Percent(current, total)} percent, " +
                   ReadyCurses(current, curseCost, total);
        }

        public static string FervourStatus(
            float current, float total, float curseCost, bool unlimited)
        {
            if (unlimited) return "Fervour unlimited";
            if (current <= ComparisonTolerance)
                return "Fervour empty, no curses ready";
            if (total > ComparisonTolerance && current >= total - ComparisonTolerance)
                return $"Fervour full, {ReadyCurses(current, curseCost)}";

            return $"Fervour {Percent(current, total)} percent, " +
                   ReadyCurses(current, curseCost, total);
        }

        private static string SpecialHeartGain(HealthSnapshot before, HealthSnapshot after)
        {
            // TotalSpiritHearts also fills the new slot. Prefer that semantic change
            // over separately reporting the resulting SpiritHearts setter.
            var spiritCapacityGain = after.SpiritCapacity - before.SpiritCapacity;
            if (spiritCapacityGain > ComparisonTolerance)
                return $"Gained {HeartCount(spiritCapacityGain, "spirit")}";

            var blue = after.Blue - before.Blue;
            if (blue > ComparisonTolerance) return $"Gained {HeartCount(blue, "blue")}";
            var black = after.Black - before.Black;
            if (black > ComparisonTolerance) return $"Gained {HeartCount(black, "black")}";
            var fire = after.Fire - before.Fire;
            if (fire > ComparisonTolerance) return $"Gained {HeartCount(fire, "fire")}";
            var ice = after.Ice - before.Ice;
            if (ice > ComparisonTolerance) return $"Gained {HeartCount(ice, "ice")}";
            return string.Empty;
        }

        private static string HeartCount(float healthPoints, string kind)
        {
            var hearts = healthPoints / HealthPointsPerHeart;
            var noun = Math.Abs(hearts - 1f) <= ComparisonTolerance ? "heart" : "hearts";
            return $"{Number(hearts)} {kind} {noun}";
        }

        private static void AddSpecial(List<string> parts, string kind, float healthPoints)
        {
            if (healthPoints > ComparisonTolerance)
                parts.Add($"{kind} {HealthValue(healthPoints)}");
        }

        private static string ReadyCurses(float current, float curseCost, float total = -1f)
        {
            if (curseCost <= ComparisonTolerance) return "curse cost unavailable";

            var ready = Math.Max(0, (int)Math.Floor(current / curseCost));
            if (total <= ComparisonTolerance)
            {
                var readyNoun = ready == 1 ? "curse" : "curses";
                return $"{ready} {readyNoun} ready";
            }

            var maximum = Math.Max(0, (int)Math.Floor(total / curseCost));
            var capacityNoun = maximum == 1 ? "curse" : "curses";
            return $"{ready} of {maximum} {capacityNoun} ready";
        }

        private static int Percent(float current, float total)
        {
            if (total <= ComparisonTolerance) return 0;
            return Math.Max(0, Math.Min(100, (int)Math.Round(current / total * 100f)));
        }

        private static string HealthValue(float healthPoints) =>
            Number(healthPoints / HealthPointsPerHeart);

        private static string Number(float value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
