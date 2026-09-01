using System.Collections.Generic;
using CultAccess.Localization;

namespace CultAccess.Status
{
    /// <summary>
    /// Everything the game draws about one follower, already resolved to words and whole
    /// percents by the caller.
    ///
    /// Deliberately a plain data carrier with no game types on it, so the wording below can be
    /// exercised in the offline harness. The <c>*Shown</c> flags matter as much as the values:
    /// the game hides several of these bars depending on the follower and the screen, and a
    /// bar that is not on the player's screen must not be in their ears. The clearest case is
    /// tiredness, which <c>FollowerInformationBox</c> computes and then hides unconditionally —
    /// it is therefore absent from this type entirely rather than present and suppressed.
    /// </summary>
    public sealed class FollowerSnapshot
    {
        /// <summary>Cleaned of the colour and trait-sprite markup the game wraps names in.</summary>
        public string Name = string.Empty;

        /// <summary>The game's own localised role name, or empty when it has none.</summary>
        public string Role = string.Empty;

        public int Level = 1;
        public bool Alive = true;

        /// <summary>
        /// The localised name of the follower's <c>CursedState</c> — ill, injured, dissenting,
        /// starving and so on. This is the same value that decides which warning icon floats
        /// over their head and which face they wear.
        /// </summary>
        public string Condition = string.Empty;

        /// <summary>
        /// The game's own one-line answer to "what is wrong with this follower", from the
        /// synthetic <c>BiggestNeed_*</c> thought the summary screen adds.
        /// </summary>
        public string BiggestNeed = string.Empty;

        /// <summary>What they are doing, from the current task type.</summary>
        public string Task = string.Empty;

        /// <summary>
        /// The game's own authored title for this follower's form — goat, deer, snake. What a
        /// sighted player identifies them by before reading any name.
        /// </summary>
        public string Species = string.Empty;

        /// <summary>
        /// The one thing worth walking over for, in the order the game's own interaction label
        /// ranks them: protect, catch, absolve, complete a quest, collect a reward, or answer
        /// a follower who is asking for you.
        /// </summary>
        public string Headline = string.Empty;

        /// <summary>Localised reason this follower cannot be chosen on the current screen.</summary>
        public string Unavailable = string.Empty;

        public bool LoyaltyShown;
        public int Loyalty;
        public bool NeedsShown;
        public int Food;
        public int Health;
        public bool PleasureShown;
        public int Pleasure;

        public int TraitCount;
        public bool Disciple;
        public bool MarriedToLeader;
        public string Spouse = string.Empty;
        public int Age;
        public int MemberDays;
    }

    /// <summary>
    /// Wording for a single follower. Pure, so it is tested without the game.
    ///
    /// Two shapes, because two callers want different lengths. <see cref="Card"/> is what a
    /// focused follower tile says, where the player is stepping a list and needs the name
    /// first and little else. <see cref="Detail"/> is what the follower key says, where they
    /// have asked about one follower specifically and everything is wanted.
    ///
    /// Name leads in both. A list of tiles that all began with a level or a role would share
    /// their first word, and the part that tells them apart would arrive last.
    /// </summary>
    public static class FollowerStatusText
    {
        /// <summary>
        /// Who this is, in the shape a list entry wants: the name first, then the species and
        /// level that tell two followers apart when several share a corner of the base.
        ///
        /// The species is what a sighted player reads off the follower's own body before
        /// anything else, and it is the game's own authored title for that form rather than
        /// a word of ours. Level 1 is left unsaid: everyone starts there, so it distinguishes
        /// nobody and would put the same three words in front of every entry in the list.
        /// </summary>
        public static string Identity(string name, string species, int level)
        {
            if (string.IsNullOrEmpty(name)) return Strings.Get("follower.unknown");

            var hasSpecies = !string.IsNullOrEmpty(species);
            if (level > 1)
                return hasSpecies
                    ? Strings.Format("follower.identity_species_level", name, level, species)
                    : Strings.Format("follower.identity_level", name, level);

            return hasSpecies
                ? Strings.Format("follower.identity_species", name, species)
                : name;
        }

        /// <summary>
        /// A follower's entry in the target list: who they are, what is wrong with them, and
        /// what you could do about it if you walked over. Distance and bearing are appended by
        /// the catalogue afterwards, so state sits before position here exactly as it does for
        /// a locked door or an unavailable interaction — identity, then what you are deciding
        /// on, then the position that confirms it.
        /// </summary>
        public static string TargetEntry(
            string identity, string condition, string headline, bool detailed)
        {
            var parts = new List<string>(4) { identity };

            if (detailed)
            {
                if (!string.IsNullOrEmpty(condition)) parts.Add(condition);
                if (!string.IsNullOrEmpty(headline)) parts.Add(headline);
            }

            return string.Join(", ", parts.ToArray());
        }

        /// <summary>The focused-tile reading. Short by design.</summary>
        public static string Card(FollowerSnapshot follower, bool detailed)
        {
            if (follower == null || follower.Name.Length == 0)
                return Strings.Get("follower.unknown");

            var parts = new List<string>(8)
            {
                Identity(follower.Name, follower.Species, follower.Level),
            };

            if (!follower.Alive) parts.Add(Strings.Get("follower.dead"));
            if (follower.Unavailable.Length > 0) parts.Add(follower.Unavailable);

            if (!detailed)
                return Join(parts);

            if (follower.Role.Length > 0) parts.Add(follower.Role);
            if (follower.Condition.Length > 0) parts.Add(follower.Condition);

            AddBars(parts, follower);
            return Join(parts);
        }

        /// <summary>The on-demand reading for one follower, in full.</summary>
        public static string Detail(FollowerSnapshot follower)
        {
            if (follower == null || follower.Name.Length == 0)
                return Strings.Get("follower.unknown");

            var parts = new List<string>(12)
            {
                Identity(follower.Name, follower.Species, follower.Level),
            };

            if (!follower.Alive) parts.Add(Strings.Get("follower.dead"));
            if (follower.Role.Length > 0) parts.Add(follower.Role);
            if (follower.Condition.Length > 0) parts.Add(follower.Condition);

            AddBars(parts, follower);

            if (follower.Headline.Length > 0) parts.Add(follower.Headline);
            if (follower.BiggestNeed.Length > 0)
                parts.Add(Strings.Format("follower.needs", follower.BiggestNeed));
            if (follower.Task.Length > 0)
                parts.Add(Strings.Format("follower.doing", follower.Task));

            if (follower.TraitCount > 0)
                parts.Add(Strings.Plural(
                    "follower.trait", "follower.traits", follower.TraitCount));
            if (follower.Disciple) parts.Add(Strings.Get("follower.disciple"));

            if (follower.MarriedToLeader) parts.Add(Strings.Get("follower.married_to_you"));
            else if (follower.Spouse.Length > 0)
                parts.Add(Strings.Format("follower.spouse", follower.Spouse));

            if (follower.Age > 0) parts.Add(Strings.Format("follower.age", follower.Age));
            parts.Add(follower.MemberDays <= 0
                ? Strings.Get("follower.member_new")
                : Strings.Plural(
                    "follower.member_day", "follower.member_days", follower.MemberDays));

            if (follower.Unavailable.Length > 0) parts.Add(follower.Unavailable);

            return Join(parts);
        }

        /// <summary>
        /// The three bars a sighted player reads off the follower card as coloured fills.
        /// Food and health are hidden together with the live brain — a dead or absent
        /// follower's card shows loyalty only — so they share one flag rather than three.
        /// </summary>
        private static void AddBars(List<string> parts, FollowerSnapshot follower)
        {
            if (follower.LoyaltyShown)
                parts.Add(Strings.Format("follower.loyalty", follower.Loyalty));

            if (follower.NeedsShown)
            {
                parts.Add(Strings.Format("follower.food", follower.Food));
                parts.Add(Strings.Format("follower.health", follower.Health));
            }

            if (follower.PleasureShown)
                parts.Add(Strings.Format("follower.pleasure", follower.Pleasure));
        }

        private static string Join(List<string> parts) =>
            string.Join(", ", parts.ToArray());
    }
}
