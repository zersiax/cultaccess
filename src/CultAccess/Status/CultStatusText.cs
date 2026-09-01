using System.Collections.Generic;
using CultAccess.Localization;

namespace CultAccess.Status
{
    /// <summary>Which of the game's four cult-wide bars a value came from.</summary>
    public enum CultBarKind
    {
        Faith,
        Food,
        Cleanliness,
        Warmth,
    }

    /// <summary>
    /// One cult bar, as the HUD would draw it.
    ///
    /// <see cref="Normalised"/> is the game's own fill fraction, and every one of the four is
    /// oriented so that full is good: faith is <c>CurrentFaith / 85</c>, food is the mean of
    /// satiation minus starvation, cleanliness is <c>1 - waste / worst-ever-waste</c>, warmth
    /// is warmth over its maximum. That shared orientation is what lets one sentence shape
    /// cover all four.
    ///
    /// <see cref="Shown"/> mirrors the matching <c>DataManager.ShowCult*</c> reveal flag. A bar
    /// the player has not unlocked is not on their screen, so it must not be in their ears
    /// either.
    /// </summary>
    public readonly struct CultBar
    {
        public CultBar(CultBarKind kind, float normalised, bool shown, bool locked)
        {
            Kind = kind;
            Normalised = normalised;
            Shown = shown;
            Locked = locked;
        }

        public CultBarKind Kind { get; }
        public float Normalised { get; }
        public bool Shown { get; }

        /// <summary>
        /// The padlock the HUD draws over a bar whose value the game has frozen —
        /// <c>BrainWashed</c> for faith, <c>Fasting</c> for food, <c>LockedWarmth</c> for
        /// warmth. A locked bar does not move, so a low reading on one is not a warning.
        /// </summary>
        public bool Locked { get; }

        public bool Low => Shown && !Locked && Normalised < CultStatusText.LowThreshold;
    }

    /// <summary>A point-in-time copy of everything the cult HUD and the Cult tab display.</summary>
    public readonly struct CultStatusSnapshot
    {
        public CultStatusSnapshot(
            CultBar faith, CultBar food, CultBar cleanliness, CultBar warmth,
            int followers, int dead)
        {
            Faith = faith;
            Food = food;
            Cleanliness = cleanliness;
            Warmth = warmth;
            Followers = followers;
            Dead = dead;
        }

        public CultBar Faith { get; }
        public CultBar Food { get; }
        public CultBar Cleanliness { get; }
        public CultBar Warmth { get; }
        public int Followers { get; }
        public int Dead { get; }

        public CultBar[] Bars => new[] { Faith, Food, Cleanliness, Warmth };
    }

    /// <summary>
    /// Wording for the cult-wide bars. Pure, so it is tested without the game.
    ///
    /// The design turns on one fact from the decompile: each bar has a mechanical consequence
    /// at the same fraction, and the game announces that consequence to sighted players by
    /// making the bar pulse. `CultFaithManager`, `HungerBar` and `IllnessBar` each run a
    /// simulation step that, below a quarter full, picks a random follower and makes them a
    /// dissenter, starving, or ill. So the crossing is the event worth speaking, and the value
    /// alone is not: a bar sitting at 60 percent has no news in it.
    ///
    /// Warmth is deliberately the exception. Its own `UpdateSimulation` is a no-op, so nothing
    /// is claimed about what a low reading will do — saying "followers will freeze" would be
    /// inventing a mechanic the code does not contain.
    /// </summary>
    public static class CultStatusText
    {
        /// <summary>
        /// The fraction every bar's pulse and every attrition rule share. Written once here
        /// because it is one constant in the game repeated in four places, and a copy that
        /// drifted would make the mod warn at a different moment from the screen.
        /// </summary>
        public const float LowThreshold = 0.25f;

        /// <summary>Full on-demand readout: every revealed bar, then the population.</summary>
        public static string Status(CultStatusSnapshot snapshot)
        {
            var parts = new List<string>(5);

            foreach (var bar in snapshot.Bars)
            {
                if (!bar.Shown) continue;
                parts.Add(bar.Locked
                    ? Strings.Format("cult.bar_locked", Name(bar.Kind), Percent(bar.Normalised))
                    : Strings.Format("cult.bar", Name(bar.Kind), Percent(bar.Normalised)));
            }

            if (parts.Count == 0) return Strings.Get("cult.not_started");

            parts.Add(Population(snapshot));
            return string.Join(". ", parts.ToArray()) + ".";
        }

        /// <summary>
        /// The terse clause the where-am-I key appends. Empty whenever nothing is wrong, which
        /// is the usual case — it exists so that a readout the player asks for constantly does
        /// not grow by four bars they can already hear about when it matters.
        /// </summary>
        public static string Alerts(CultStatusSnapshot snapshot)
        {
            var low = new List<string>(4);
            foreach (var bar in snapshot.Bars)
                if (bar.Low) low.Add(Name(bar.Kind));

            return low.Count == 0
                ? string.Empty
                : Strings.Format("cult.alert", string.Join(", ", low.ToArray()));
        }

        /// <summary>
        /// Spoken once when a bar crosses the quarter mark in either direction. Leads with the
        /// bar and the direction, because that is what the listener is deciding on; the number
        /// and the consequence follow.
        /// </summary>
        public static string Crossing(CultBarKind kind, float normalised, bool low)
        {
            if (!low)
                return Strings.Format(
                    "cult.recovered", Name(kind), Percent(normalised));

            var consequence = Consequence(kind);
            return consequence.Length == 0
                ? Strings.Format("cult.low", Name(kind), Percent(normalised))
                : Strings.Format(
                    "cult.low_with_consequence", Name(kind), Percent(normalised), consequence);
        }

        /// <summary>Spoken when the game freezes or releases a bar.</summary>
        public static string LockChanged(CultBarKind kind, bool locked) =>
            Strings.Format(locked ? "cult.locked" : "cult.unlocked", Name(kind));

        public static string Name(CultBarKind kind)
        {
            switch (kind)
            {
                case CultBarKind.Faith: return Strings.Get("cult.faith");
                case CultBarKind.Food: return Strings.Get("cult.food");
                case CultBarKind.Cleanliness: return Strings.Get("cult.cleanliness");
                default: return Strings.Get("cult.warmth");
            }
        }

        /// <summary>
        /// What the game does next if this bar stays low, taken from the matching
        /// <c>UpdateSimulation</c>. Empty for warmth, whose simulation step does nothing.
        /// </summary>
        private static string Consequence(CultBarKind kind)
        {
            switch (kind)
            {
                case CultBarKind.Faith: return Strings.Get("cult.consequence_faith");
                case CultBarKind.Food: return Strings.Get("cult.consequence_food");
                case CultBarKind.Cleanliness: return Strings.Get("cult.consequence_cleanliness");
                default: return string.Empty;
            }
        }

        private static string Population(CultStatusSnapshot snapshot)
        {
            var living = Strings.Plural(
                "cult.follower", "cult.followers", snapshot.Followers);
            return snapshot.Dead <= 0
                ? living
                : Strings.Format("cult.population_with_dead", living, snapshot.Dead);
        }

        /// <summary>
        /// Rounded to whole percent and clamped, because the underlying floats drift a little
        /// either side of their own limits and "faith 101 percent" would read as a bug.
        /// </summary>
        internal static int Percent(float normalised)
        {
            var value = (int)System.Math.Round(normalised * 100f);
            if (value < 0) return 0;
            return value > 100 ? 100 : value;
        }
    }
}
