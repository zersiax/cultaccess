using System;
using System.Collections.Generic;
using System.Reflection;
using CultAccess.Diagnostics;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;
using Lamb.UI.PauseDetails;
using Rewired;
using UnityEngine.UI;
using src.UINavigator;

namespace CultAccess.UI
{
    /// <summary>Reads the game-owned name, effect, lore, and position of tarot UI controls.</summary>
    internal static class TarotCardDescriber
    {
        private static readonly FieldInfo CollectionItemsField =
            AccessTools.Field(typeof(UITarotCardsMenuController), "_items");
        private static readonly FieldInfo CollectionShownCardField =
            AccessTools.Field(typeof(UITarotCardsMenuController), "_showCard");
        private static readonly FieldInfo ChoiceButton1Field =
            AccessTools.Field(typeof(UITarotChoiceOverlayController), "_button1");
        private static readonly FieldInfo ChoiceButton2Field =
            AccessTools.Field(typeof(UITarotChoiceOverlayController), "_button2");
        private static readonly FieldInfo ChoiceCard1Field =
            AccessTools.Field(typeof(UITarotChoiceOverlayController), "_card1");
        private static readonly FieldInfo ChoiceCard2Field =
            AccessTools.Field(typeof(UITarotChoiceOverlayController), "_card2");
        private static readonly FieldInfo PickupCardField =
            AccessTools.Field(typeof(UITarotPickUpOverlayController), "_card");
        private static readonly FieldInfo[] RewardCardFields =
        {
            AccessTools.Field(typeof(UIFleeceTarotRewardOverlayController), "_card1"),
            AccessTools.Field(typeof(UIFleeceTarotRewardOverlayController), "_card2"),
            AccessTools.Field(typeof(UIFleeceTarotRewardOverlayController), "_card3"),
            AccessTools.Field(typeof(UIFleeceTarotRewardOverlayController), "_card4"),
        };

        private static readonly HashSet<string> ReportedReflectionFailures =
            new HashSet<string>();

        internal static bool TryDescribe(Selectable selectable, out string description) =>
            TryDescribe(selectable, includeLore: false, out description);

        internal static bool TryDescribeContext(Selectable selectable, out string context)
        {
            context = string.Empty;
            if (selectable == null) return false;

            if (selectable.GetComponentInParent<UITarotChoiceOverlayController>() != null)
            {
                context = $"Choose a tarot card. {HorizontalControls()} review choices. " +
                          $"{AcceptControl()} selects";
                return true;
            }

            if (selectable.GetComponentInParent<UITarotCardsMenuController>() != null)
            {
                context = $"Tarot card collection. Direction keys review cards. " +
                          $"{CancelControl()} closes";
                return true;
            }

            return false;
        }

        internal static bool TryDescribePanel(UIMenuBase menu, out string description)
        {
            description = string.Empty;
            if (menu == null) return false;

            var current = MonoSingleton<UINavigatorNew>.Instance?.CurrentSelectable?.Selectable;
            if (current != null && current.transform.IsChildOf(menu.transform) &&
                TryDescribe(current, includeLore: true, out description))
                return true;

            if (menu is UITarotCardsMenuController collection)
            {
                var shown = Read<TarotCards.TarotCard>(
                    collection, CollectionShownCardField, "tarot collection shown card");
                if (shown != null && shown.CardType != TarotCards.Card.Count)
                {
                    description = DescribeCard(shown, includeLore: true);
                    return description.Length > 0;
                }

                description = "No tarot card currently selected";
                return true;
            }

            if (menu is UITarotPickUpOverlayController pickup)
            {
                var card = PickupCard(pickup);
                description = card == null
                    ? "Tarot card details unavailable"
                    : DescribeCard(card, includeLore: true);
                return true;
            }

            if (menu is UIFleeceTarotRewardOverlayController rewards)
            {
                description = DescribeRewards(rewards, includeLore: true);
                return true;
            }

            if (menu is UITarotChoiceOverlayController)
            {
                description = "No tarot card choice currently selected";
                return true;
            }

            return false;
        }

        internal static string DescribeCard(
            TarotCards.TarotCard card, bool includeLore = false)
        {
            if (card == null || card.CardType == TarotCards.Card.Count)
                return "Tarot card details unavailable";

            var parts = new List<string>();
            try
            {
                parts.Add(CardName(card));

                var player = DescriptionPlayer();
                var descriptionTerm = $"TarotCards/{card.CardType}/Description" +
                                      (card.UpgradeIndex > 0 ? card.UpgradeIndex.ToString() : string.Empty);
                var effect = RichText.Clean(TarotCards.LocalisedDescription(
                    card.CardType, card.UpgradeIndex, player));
                if (RichText.IsUsableLocalization(effect, descriptionTerm) &&
                    !parts.Contains(effect))
                    parts.Add(effect);

                if (includeLore)
                {
                    var loreTerm = $"TarotCards/{card.CardType}/Lore";
                    var lore = RichText.Clean(TarotCards.LocalisedLore(card.CardType));
                    if (RichText.IsUsableLocalization(lore, loreTerm) &&
                        !parts.Contains(lore))
                        parts.Add(lore);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning(
                    $"Could not describe tarot card {card.CardType}: {e.Message}");
                if (parts.Count == 0) parts.Add(RichText.Humanise(card.CardType.ToString()));
            }

            return string.Join(". ", parts.ToArray());
        }

        internal static string CardName(TarotCards.TarotCard card)
        {
            if (card == null || card.CardType == TarotCards.Card.Count)
                return "Unknown tarot card";

            try
            {
                var term = $"TarotCards/{card.CardType}/Name";
                var name = RichText.Clean(
                    TarotCards.LocalisedName(card.CardType, card.UpgradeIndex));
                return RichText.IsUsableLocalization(name, term)
                    ? name
                    : RichText.Humanise(card.CardType.ToString());
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning(
                    $"Could not read tarot card name {card.CardType}: {e.Message}");
                return RichText.Humanise(card.CardType.ToString());
            }
        }

        internal static TarotCards.TarotCard PickupCard(
            UITarotPickUpOverlayController pickup) =>
            Read<TarotCards.TarotCard>(pickup, PickupCardField, "tarot pickup card");

        internal static TarotCards.TarotCard ShownCollectionCard(
            UITarotCardsMenuController collection) =>
            Read<TarotCards.TarotCard>(
                collection, CollectionShownCardField, "tarot collection shown card");

        internal static List<TarotCards.TarotCard> RewardCards(
            UIFleeceTarotRewardOverlayController rewards)
        {
            var cards = new List<TarotCards.TarotCard>();
            for (var index = 0; index < RewardCardFields.Length; index++)
            {
                var card = Read<TarotCards.TarotCard>(
                    rewards, RewardCardFields[index], $"tarot reward card {index + 1}");
                if (card != null && card.CardType != TarotCards.Card.Count) cards.Add(card);
            }

            return cards;
        }

        internal static string DescribeRewards(
            UIFleeceTarotRewardOverlayController rewards, bool includeLore = false)
        {
            var cards = RewardCards(rewards);
            if (cards.Count == 0) return "Tarot rewards unavailable";

            var parts = new List<string> { $"Tarot rewards, {cards.Count} cards" };
            for (var index = 0; index < cards.Count; index++)
                parts.Add($"Card {index + 1}, {DescribeCard(cards[index], includeLore)}");
            return string.Join(". ", parts.ToArray());
        }

        internal static string AcceptControl() => GameControl(38, "confirm");

        internal static string CancelControl() => GameControl(39, "back");

        internal static string AcceptOrCancelControls() =>
            $"{AcceptControl()} or {CancelControl()}";

        private static bool TryDescribe(
            Selectable selectable, bool includeLore, out string description)
        {
            description = string.Empty;
            if (selectable == null) return false;

            var item = selectable.GetComponentInParent<TarotCardItemBase>();
            if (item != null)
            {
                if (item is TarotCardItem_Unlocked && !TarotCards.IsUnlocked(item.Type))
                    description = "Locked tarot card";
                else
                    description = DescribeCard(item.Card, includeLore);

                var collection = item.GetComponentInParent<UITarotCardsMenuController>();
                var items = Read<List<TarotCardItem_Unlocked>>(
                    collection, CollectionItemsField, "tarot collection items");
                var index = items?.IndexOf(item as TarotCardItem_Unlocked) ?? -1;
                if (index >= 0)
                    description += $". Card {index + 1} of {items.Count}";
                else if (item is TarotCardItem_Run)
                    description += ". Active tarot card";
                return true;
            }

            var choice = selectable.GetComponentInParent<UITarotChoiceOverlayController>();
            if (choice == null) return false;

            var button = selectable as MMButton ?? selectable.GetComponentInParent<MMButton>();
            var button1 = Read<MMButton>(choice, ChoiceButton1Field, "tarot choice button 1");
            var button2 = Read<MMButton>(choice, ChoiceButton2Field, "tarot choice button 2");
            TarotCards.TarotCard card;
            int position;
            if (button == button1)
            {
                card = Read<TarotCards.TarotCard>(choice, ChoiceCard1Field, "tarot choice card 1");
                position = 1;
            }
            else if (button == button2)
            {
                card = Read<TarotCards.TarotCard>(choice, ChoiceCard2Field, "tarot choice card 2");
                position = 2;
            }
            else
            {
                return false;
            }

            description = $"{DescribeCard(card, includeLore)}. " +
                          $"Choice {position} of 2. {AcceptControl()} selects";
            return true;
        }

        private static string HorizontalControls()
        {
            var left = BindingDump.KeyboardBindingsForAction(35, Pole.Negative);
            var right = BindingDump.KeyboardBindingsForAction(35, Pole.Positive);
            if (left.Length == 0) left = "left";
            if (right.Length == 0) right = "right";
            return $"{left} and {right}";
        }

        private static string GameControl(int action, string fallback)
        {
            var binding = BindingDump.KeyboardBindingsForAction(action);
            return binding.Length > 0 ? binding : fallback;
        }

        private static PlayerFarming DescriptionPlayer()
        {
            try
            {
                var player = MonoSingleton<UINavigatorNew>.Instance?.AllowInputOnlyFromPlayer;
                if (player != null) return player;
            }
            catch
            {
                // A menu can be instantiated before the navigator finishes transferring input.
            }

            return PlayerFarming.Instance;
        }

        private static T Read<T>(object owner, FieldInfo field, string context)
            where T : class
        {
            if (owner == null) return null;
            if (field == null)
            {
                ReportOnce(context, "field not found");
                return null;
            }

            try { return field.GetValue(owner) as T; }
            catch (Exception e)
            {
                ReportOnce(context, e.Message);
                return null;
            }
        }

        private static void ReportOnce(string context, string reason)
        {
            if (!ReportedReflectionFailures.Add(context)) return;
            Plugin.Log.LogWarning($"Could not read {context}: {reason}");
        }
    }
}
