using System.Collections.Generic;
using CultAccess.Util;
using Lamb.UI;
using UnityEngine.UI;

namespace CultAccess.UI
{
    /// <summary>
    /// Reads the follower indoctrination pickers: the form and variant grids, and the trait
    /// list.
    ///
    /// These are the purest icon-only controls found so far. A form tile is a rendered
    /// character and nothing else, so the generic reader announced the literal word "button";
    /// a trait tile announced its own GameObject name, "Trait Item(Clone)". Both are on the
    /// path every new follower goes through, and both were unusable.
    ///
    /// This matters twice over with the Twitch integration connected, because the same picker
    /// feeds <c>TwitchVoting</c>: chat votes on the choice. Unlabelled tiles mean neither
    /// knowing what you are choosing nor what chat chose for you.
    ///
    /// Neither tile carries its own name, but both carry the identifier the game looks its name
    /// up by. <c>IndoctrinationCharacterItem</c> exposes <c>Skin</c>, which
    /// <c>WorshipperData.GetCharacters</c> resolves to the authored <c>Title</c>, and
    /// <c>DropLocation</c>, which says which biome the form came from.
    /// <c>IndoctrinationTraitItem</c> exposes <c>TraitType</c>, and <c>FollowerTrait</c> has
    /// localised titles and descriptions for it.
    /// </summary>
    internal static class IndoctrinationDescriber
    {
        internal static bool TryDescribe(Selectable selectable, out string description)
        {
            description = string.Empty;
            if (selectable == null) return false;

            var trait = selectable.GetComponentInParent<IndoctrinationTraitItem>();
            if (trait != null)
            {
                description = DescribeTrait(trait);
                return true;
            }

            // Five sibling classes, one per grid, all deriving from the generic
            // IndoctrinationCharacterItem<T>. The generic base cannot be resolved by
            // GetComponentInParent without naming its type argument, so each concrete grid is
            // asked in turn. Form is the one the player names most often; the others share its
            // shape exactly.
            if (TryCharacterItem<IndoctrinationFormItem>(selectable, "form", out description) ||
                TryCharacterItem<IndoctrinationVariantItem>(selectable, "variant", out description) ||
                TryCharacterItem<IndoctrinationColourItem>(selectable, "colour", out description) ||
                TryCharacterItem<IndoctrinationOutfitItem>(selectable, "outfit", out description) ||
                TryCharacterItem<necklaceFormItem>(selectable, "necklace", out description))
                return true;

            return false;
        }

        private static bool TryCharacterItem<T>(Selectable selectable, string kind, out string description)
            where T : IndoctrinationCharacterItem<T>
        {
            description = string.Empty;
            var item = selectable.GetComponentInParent<T>();
            if (item == null) return false;

            description = DescribeForm(kind, item.Skin, item.Locked);
            return true;
        }

        private static string DescribeTrait(IndoctrinationTraitItem item)
        {
            var type = item.TraitType;
            var parts = new List<string>(3)
            {
                CleanOrFallback(
                    FollowerTrait.GetLocalizedTitle(type),
                    type.ToString(),
                    $"Traits/{type}/Name"),
            };

            // The follower summary screen builds two grids from this same tile type — the
            // follower's own traits and the cult's — under headings a screen reader never
            // reaches. Without this qualifier the two read identically, which turns a missing
            // heading into a wrong answer about whose trait it is.
            var cultTrait = FollowerCardDescriber.IsCultTrait(item);
            if (cultTrait) parts.Add(Localization.Strings.Get("follower.cult_trait"));

            if (SelectableDescriber.Verbosity != Verbosity.Low)
            {
                // The brain overload personalises the wording for a follower that already
                // exists; there is none yet at indoctrination, so the generic text is right.
                var prose = FollowerTrait.GetLocalizedDescription(type);
                if (RichText.IsUsableLocalization(prose))
                    parts.Add(RichText.Clean(prose));
            }

            var description = string.Join(", ", parts.ToArray());
            Plugin.Log.LogInfo(
                $"[indoctrination] kind=trait type={type} cult={cultTrait} " +
                $"spoken=\"{description}\"");
            return description;
        }

        /// <summary>
        /// Forms and variants share <c>IndoctrinationCharacterItem</c>, so one description
        /// covers both grids. A locked tile is named rather than hidden: the grid still holds
        /// its place, and silence there would read as a gap in the list.
        /// </summary>
        private static string DescribeForm(string kind, string skin, bool locked)
        {
            var parts = new List<string>(4) { FormName(skin) };

            if (SelectableDescriber.Verbosity != Verbosity.Low)
            {
                if (locked) parts.Add("locked");

                var origin = Origin(skin);
                if (origin != null) parts.Add(origin);
            }

            var description = string.Join(", ", parts.ToArray());
            Plugin.Log.LogInfo(
                $"[indoctrination] kind={kind} skin=\"{skin}\" locked={locked} " +
                $"spoken=\"{description}\"");
            return description;
        }

        private static string FormName(string skin)
        {
            var data = Characters(skin);
            if (data != null && !string.IsNullOrEmpty(data.Title) &&
                data.Title != "Character Name")
                return RichText.Clean(data.Title);

            // The skin identifier is the last resort, humanised. It is an internal name, but a
            // wrong-sounding name still tells the player which tile they are on, and an
            // unnamed tile does not.
            return string.IsNullOrEmpty(skin) ? "unknown form" : RichText.Humanise(skin);
        }

        /// <summary>
        /// Where the form is found. Spoken because the grid is grouped by it, so it is the
        /// player's only sense of position in a list with no headings they can see.
        /// </summary>
        private static string Origin(string skin)
        {
            var data = Characters(skin);
            if (data == null) return null;

            switch (data.DropLocation)
            {
                case WorshipperData.DropLocation.Dungeon1: return "from Darkwood";
                case WorshipperData.DropLocation.Dungeon2: return "from Anura";
                case WorshipperData.DropLocation.Dungeon3: return "from Anchordeep";
                case WorshipperData.DropLocation.Dungeon4: return "from Silk Cradle";
                case WorshipperData.DropLocation.SpecialEvents: return "special event";
                case WorshipperData.DropLocation.DLC:
                case WorshipperData.DropLocation.Major_DLC: return "downloadable content";
                default: return null;
            }
        }

        private static WorshipperData.SkinAndData Characters(string skin)
        {
            if (string.IsNullOrEmpty(skin)) return null;

            try
            {
                return WorshipperData.Instance?.GetCharacters(skin);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        private static string CleanOrFallback(
            string localized, string fallback, string expectedTerm)
        {
            if (RichText.IsUsableLocalization(localized, expectedTerm))
                return RichText.Clean(localized);
            return RichText.Humanise(fallback);
        }
    }
}
