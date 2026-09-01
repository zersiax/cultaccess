using CultAccess.Status;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;
using UnityEngine;
using UnityEngine.UI;
using src.UI.Items;

namespace CultAccess.UI
{
    /// <summary>
    /// Reads the follower tiles and the follower summary screen.
    ///
    /// One adapter reaches every follower picker in the game because they all instantiate one
    /// tile. <c>FollowerInformationBox</c> is what the roster, the sacrifice picker, daycare
    /// assignment, mating, beds, the healing bay, knucklebones, the confession booth and the
    /// rest put on screen, and it carries the <c>FollowerInfo</c> and the
    /// <c>FollowerSelectEntry</c> behind it. Writing one describer per screen would have meant
    /// more than a dozen of them.
    ///
    /// What those tiles lose without this is not their names — those are text — but the four
    /// coloured fills beside them, the 38 localised reasons a follower cannot be chosen, and,
    /// on the summary screen that "read mind" opens, which of two identical-looking trait
    /// grids belongs to the follower and which to the cult.
    /// </summary>
    internal static class FollowerCardDescriber
    {
        /// <summary>
        /// The thought row's arrow icon and the four sprites it chooses between. Read rather
        /// than recomputed from <c>ThoughtData.Modifier</c> for two reasons. The arrow is all
        /// a sighted player gets — the card carries no number and no tooltip — so naming the
        /// arrow is equal access while naming the modifier would be more than it. And reading
        /// a field costs this one description if a game update renames it, where a Harmony
        /// patch on a method that no longer resolves aborts <c>PatchAll</c> and silences the
        /// entire mod.
        /// </summary>
        private static readonly AccessTools.FieldRef<FollowerThoughtItem, Image> ThoughtIconRef =
            SafeField<FollowerThoughtItem, Image>("_icon");
        private static readonly AccessTools.FieldRef<FollowerThoughtItem, Sprite> FaithUpRef =
            SafeField<FollowerThoughtItem, Sprite>("_faithUp");
        private static readonly AccessTools.FieldRef<FollowerThoughtItem, Sprite> FaithDoubleUpRef =
            SafeField<FollowerThoughtItem, Sprite>("_faithDoubleUp");
        private static readonly AccessTools.FieldRef<FollowerThoughtItem, Sprite> FaithDownRef =
            SafeField<FollowerThoughtItem, Sprite>("_faithDown");

        /// <summary>The game's own field name carries this typo; matching it is required.</summary>
        private static readonly AccessTools.FieldRef<FollowerThoughtItem, Sprite> FaithDoubleDownRef =
            SafeField<FollowerThoughtItem, Sprite>("_fiathDoubleDown");

        private static readonly AccessTools.FieldRef<UIFollowerSummaryMenuController, RectTransform>
            SummaryCultTraitsRef = SafeField<UIFollowerSummaryMenuController, RectTransform>(
                "_cultTraitContent");

        internal static bool TryDescribe(Selectable selectable, out string description)
        {
            description = string.Empty;
            if (selectable == null) return false;

            var thought = selectable.GetComponentInParent<FollowerThoughtItem>();
            if (thought != null && TryDescribeThought(thought, selectable, out description))
                return true;

            // Deliberately FollowerInformationBox rather than its base FollowerSelectItem.
            // That subclass is the one with the invisible bars, and it is what every picker
            // and the roster instantiate. Its four siblings — the dead-follower box, the
            // missionary item, the demon item and the Twitch box — carry their own prose
            // instead, which the generic reader already picks up; claiming them would replace
            // text that works with a shorter reading, which is a regression rather than a fix.
            // The [focus] adapter field will say if any of them needs one later.
            var card = selectable.GetComponentInParent<FollowerInformationBox>();
            var found = card == null ? "child" : "parent";

            // Measured 2026-08-26: on the follower summary screen the focused MMButton is
            // "Follower Item" and the box hangs *below* it, not above, so the parent search
            // missed and the generic reader announced the prefab's placeholder strings —
            // "Follower Name, Married Spouse". The roster's prefab nests the other way round,
            // which is why the same adapter worked there and not here. Search both directions
            // and record which one answered, because a prefab that changes shape again should
            // show up as a shift in this field rather than as placeholder text reaching a
            // player.
            if (card == null) card = selectable.GetComponentInChildren<FollowerInformationBox>();
            if (card == null) return false;

            var snapshot = FollowerReader.FromCard(card);
            if (snapshot == null) return false;

            description = FollowerStatusText.Card(
                snapshot, SelectableDescriber.Verbosity != Verbosity.Low);
            Plugin.Log.LogInfo(
                $"[follower card] type={card.GetType().Name} resolved={found} " +
                $"name=\"{Safe(snapshot.Name)}\" loyalty={Shown(snapshot.LoyaltyShown, snapshot.Loyalty)} " +
                $"food={Shown(snapshot.NeedsShown, snapshot.Food)} " +
                $"health={Shown(snapshot.NeedsShown, snapshot.Health)} " +
                $"pleasure={Shown(snapshot.PleasureShown, snapshot.Pleasure)} " +
                $"unavailable=\"{Safe(snapshot.Unavailable)}\" spoken=\"{Safe(description)}\"");
            return true;
        }

        /// <summary>
        /// A thought row already carries its name and description as real text, so the reading
        /// only has to add what the row shows as a picture: one of four arrow sprites standing
        /// for the size and direction of the faith the thought is worth.
        /// </summary>
        private static bool TryDescribeThought(
            FollowerThoughtItem item, Selectable selectable, out string description)
        {
            var text = RichText.Clean(SelectableDescriber.ExtractLabel(selectable));
            if (text.Length == 0)
                text = RichText.Clean(LabelFrom(item));

            var faith = ArrowKey(item);

            // Nothing to add and nothing to say: decline the row rather than claiming it with
            // an empty string, which would reach the reader as silence and be indistinguishable
            // from focus having vanished.
            if (text.Length == 0 && faith == null)
            {
                Plugin.Log.LogInfo("[follower thought] no text and no arrow, declined");
                description = string.Empty;
                return false;
            }

            description = faith == null
                ? text
                : text.Length == 0
                    ? Localization.Strings.Get(faith)
                    : $"{RichText.TrimTrailingPunctuation(text)}, {Localization.Strings.Get(faith)}";

            // The sprite's own name is logged because the first session reported "up a lot"
            // on every row it saw. That is either true — those thoughts really are worth
            // seven faith or more — or the comparison is matching the wrong field, and the
            // two are indistinguishable from the outcome alone.
            Plugin.Log.LogInfo(
                $"[follower thought] arrow={faith ?? "none"} sprite=\"{SpriteName(item)}\" " +
                $"spoken=\"{Safe(description)}\"");
            return true;
        }

        /// <summary>
        /// Whether a trait tile belongs to the cult rather than to the follower.
        ///
        /// The summary screen builds two grids out of the same <c>IndoctrinationTraitItem</c>,
        /// one for the follower's traits and one for the cult's, with a heading above each
        /// that a screen reader never reaches. Read identically they are indistinguishable,
        /// and a cult trait mistaken for a personal one is a wrong answer rather than a
        /// missing one. The Cult tab has only the cult grid, so anything under it is a cult
        /// trait outright.
        /// </summary>
        internal static bool IsCultTrait(Component tile)
        {
            if (tile == null) return false;

            try
            {
                if (tile.GetComponentInParent<CultMenu>() != null) return true;

                var summary = tile.GetComponentInParent<UIFollowerSummaryMenuController>();
                if (summary == null || SummaryCultTraitsRef == null) return false;

                var cultContent = SummaryCultTraitsRef(summary);
                if (cultContent == null) return false;

                for (var node = tile.transform; node != null; node = node.parent)
                    if (ReferenceEquals(node, cultContent)) return true;

                return false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[follower card] could not place a trait tile: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Which of the four arrows the row is showing, as a string key, or null when it
        /// cannot be told. Reference comparison against the row's own sprite fields, so the
        /// answer is whatever is actually on screen rather than a re-derived guess.
        /// </summary>
        private static string ArrowKey(FollowerThoughtItem item)
        {
            var icon = Field(ThoughtIconRef, item);
            var sprite = icon == null ? null : icon.sprite;
            if (sprite == null) return null;

            if (ReferenceEquals(sprite, Field(FaithDoubleUpRef, item)))
                return "follower.thought_up_lot";
            if (ReferenceEquals(sprite, Field(FaithUpRef, item)))
                return "follower.thought_up";
            if (ReferenceEquals(sprite, Field(FaithDoubleDownRef, item)))
                return "follower.thought_down_lot";
            if (ReferenceEquals(sprite, Field(FaithDownRef, item)))
                return "follower.thought_down";

            return null;
        }

        private static TField Field<TOwner, TField>(
            AccessTools.FieldRef<TOwner, TField> reference, TOwner owner)
            where TOwner : class =>
            reference == null || owner == null ? default : reference(owner);

        private static string LabelFrom(FollowerThoughtItem item)
        {
            foreach (var text in item.GetComponentsInChildren<TMPro.TMP_Text>(false))
                if (!string.IsNullOrEmpty(text.text)) return text.text;
            return string.Empty;
        }

        /// <summary>Diagnostic only: which sprite asset the row is actually showing.</summary>
        private static string SpriteName(FollowerThoughtItem item)
        {
            var icon = Field(ThoughtIconRef, item);
            var sprite = icon == null ? null : icon.sprite;
            return sprite == null ? "none" : sprite.name;
        }

        private static string Shown(bool shown, int value) => shown ? value.ToString() : "hidden";

        private static string Safe(string value) => value == null ? string.Empty : value;

        private static AccessTools.FieldRef<TOwner, TField> SafeField<TOwner, TField>(string name)
        {
            try { return AccessTools.FieldRefAccess<TOwner, TField>(name); }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning(
                    $"[follower card] {typeof(TOwner).Name}.{name} not found: {e.Message}");
                return null;
            }
        }
    }
}
