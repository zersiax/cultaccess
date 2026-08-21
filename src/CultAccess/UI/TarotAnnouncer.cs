using System;
using System.Collections.Generic;
using CultAccess.Speech;
using Lamb.UI;
using UnityEngine;
using src.UINavigator;

namespace CultAccess.UI
{
    /// <summary>
    /// Owns tarot presentation and run-state announcements. A presented-card gate keeps
    /// save restoration silent while still confirming cards the player just chose or took.
    /// </summary>
    internal static class TarotAnnouncer
    {
        private sealed class PresentedCard
        {
            public TarotCards.TarotCard Card;
            public PlayerFarming Player;
            public float ExpiresAt;
        }

        // Presentations may remain open while the player listens to long effects and lore.
        // Five minutes prevents abandoned UI state from matching a much later run restore.
        private const float PresentationLifetimeSeconds = 300f;

        // Fleece rewards add up to four cards in one frame. Settle them into one confirmation.
        private const float AcquisitionSettleSeconds = 0.12f;

        private static readonly List<PresentedCard> Presented = new List<PresentedCard>();
        private static readonly List<TarotCards.TarotCard> Acquired =
            new List<TarotCards.TarotCard>();
        private static readonly HashSet<UITarotCardsMenuController> UnlockPromptAnnounced =
            new HashSet<UITarotCardsMenuController>();

        private static bool _subscribed;
        private static float _acquiredDueAt;
        private static UITrinketCards _standaloneUI;
        private static TarotCards.TarotCard _standaloneCard;

        internal static void Initialize()
        {
            if (_subscribed) return;
            TrinketManager.OnTrinketAdded += OnTrinketAdded;
            TrinketManager.OnTrinketRemoved += OnTrinketRemoved;
            _subscribed = true;
        }

        internal static void Shutdown()
        {
            if (_subscribed)
            {
                TrinketManager.OnTrinketAdded -= OnTrinketAdded;
                TrinketManager.OnTrinketRemoved -= OnTrinketRemoved;
            }

            _subscribed = false;
            Presented.Clear();
            Acquired.Clear();
            UnlockPromptAnnounced.Clear();
            _standaloneUI = null;
            _standaloneCard = null;
        }

        internal static void Tick()
        {
            PrunePresented();
            if (Acquired.Count == 0 || Time.unscaledTime < _acquiredDueAt) return;

            if (!Plugin.SpeechEnabled.Value)
            {
                Acquired.Clear();
                return;
            }

            string spoken;
            if (Acquired.Count == 1)
            {
                spoken = $"Tarot card acquired: {TarotCardDescriber.CardName(Acquired[0])}";
            }
            else
            {
                var names = new List<string>();
                foreach (var card in Acquired)
                {
                    var name = TarotCardDescriber.CardName(card);
                    if (!names.Contains(name)) names.Add(name);
                }

                spoken = $"Tarot cards acquired: {string.Join(", ", names.ToArray())}";
            }

            Plugin.Log.LogInfo($"[tarot] event=acquired count={Acquired.Count} spoken=\"{spoken}\"");
            Acquired.Clear();
            Speaker.Say(spoken, SpeechPriority.Queued);
        }

        internal static void PresentPickup(
            UITarotPickUpOverlayController pickup, TarotCards.TarotCard card)
        {
            Register(card, CurrentPlayer());
            if (!Plugin.SpeechEnabled.Value) return;

            var spoken = $"Tarot card revealed. {TarotCardDescriber.DescribeCard(card)}. " +
                         $"{TarotCardDescriber.AcceptOrCancelControls()} takes this card";
            Plugin.Log.LogInfo(
                $"[tarot] event=pickup-shown card={card?.CardType.ToString() ?? "null"} " +
                $"spoken=\"{spoken}\"");
            Speaker.Say(spoken, SpeechPriority.Now);
        }

        internal static void PresentChoice(
            TarotCards.TarotCard card1, TarotCards.TarotCard card2)
        {
            var player = CurrentPlayer();
            Register(card1, player);
            Register(card2, player);
        }

        internal static void SelectChoice(TarotCards.TarotCard selected)
        {
            var player = CurrentPlayer();
            for (var index = Presented.Count - 1; index >= 0; index--)
            {
                var candidate = Presented[index];
                if (candidate.Player != null && player != null && candidate.Player != player) continue;
                if (!SameCard(candidate.Card, selected)) Presented.RemoveAt(index);
            }

            if (!Plugin.SpeechEnabled.Value) return;
            var spoken = $"Selected {TarotCardDescriber.CardName(selected)}";
            Plugin.Log.LogInfo(
                $"[tarot] event=selected card={selected?.CardType.ToString() ?? "null"} " +
                $"spoken=\"{spoken}\"");
            Speaker.Say(spoken, SpeechPriority.Now);
        }

        internal static void PresentRewards(UIFleeceTarotRewardOverlayController rewards)
        {
            var cards = TarotCardDescriber.RewardCards(rewards);
            var player = CurrentPlayer();
            foreach (var card in cards) Register(card, player);
        }

        internal static void AnnounceRewards(UIFleeceTarotRewardOverlayController rewards)
        {
            if (!Plugin.SpeechEnabled.Value) return;
            var spoken = $"{TarotCardDescriber.DescribeRewards(rewards)}. " +
                         $"{TarotCardDescriber.AcceptOrCancelControls()} continues";
            Plugin.Log.LogInfo($"[tarot] event=rewards-shown spoken=\"{spoken}\"");
            Speaker.Say(spoken, SpeechPriority.Now);
        }

        internal static void PresentStandalone(
            UITrinketCards standaloneUI, TarotCards.TarotCard card)
        {
            _standaloneUI = standaloneUI;
            _standaloneCard = card;
            Register(card, CurrentPlayer());
            if (!Plugin.SpeechEnabled.Value) return;

            var spoken = $"Tarot card revealed. {TarotCardDescriber.DescribeCard(card)}. " +
                         $"{TarotCardDescriber.AcceptOrCancelControls()} takes this card";
            Plugin.Log.LogInfo(
                $"[tarot] event=standalone-shown card={card?.CardType.ToString() ?? "null"} " +
                $"spoken=\"{spoken}\"");
            Speaker.Say(spoken, SpeechPriority.Now);
        }

        internal static bool TryReadStandalone(bool includeLore, out string description)
        {
            description = string.Empty;
            if (_standaloneUI == null || !_standaloneUI.gameObject.activeInHierarchy ||
                _standaloneCard == null)
                return false;

            description = TarotCardDescriber.DescribeCard(_standaloneCard, includeLore);
            return true;
        }

        internal static bool AnnounceStandalone()
        {
            if (!TryReadStandalone(includeLore: false, out var description)) return false;
            Speaker.Say(description, SpeechPriority.Now);
            return true;
        }

        internal static void CloseStandalone(UITrinketCards standaloneUI)
        {
            if (standaloneUI != _standaloneUI) return;
            _standaloneUI = null;
            _standaloneCard = null;
        }

        internal static void AnnounceUnlockReady(UITarotCardsMenuController menu)
        {
            if (menu == null || !UnlockPromptAnnounced.Add(menu)) return;
            var card = TarotCardDescriber.ShownCollectionCard(menu);
            if (card == null || card.CardType == TarotCards.Card.Count) return;
            if (!Plugin.SpeechEnabled.Value) return;

            var spoken = $"Tarot card unlocked. {TarotCardDescriber.DescribeCard(card)}. " +
                         $"{TarotCardDescriber.AcceptControl()} continues";
            Plugin.Log.LogInfo(
                $"[tarot] event=unlock-ready card={card.CardType} spoken=\"{spoken}\"");
            Speaker.Say(spoken, SpeechPriority.Now);
        }

        private static void Register(TarotCards.TarotCard card, PlayerFarming player)
        {
            if (card == null || card.CardType == TarotCards.Card.Count) return;
            PrunePresented();

            // Reopening the same presentation should renew it, not leave duplicate matches.
            for (var index = Presented.Count - 1; index >= 0; index--)
            {
                var existing = Presented[index];
                if (SameCard(existing.Card, card) && existing.Player == player)
                    Presented.RemoveAt(index);
            }

            Presented.Add(new PresentedCard
            {
                Card = new TarotCards.TarotCard(card.CardType, card.UpgradeIndex),
                Player = player,
                ExpiresAt = Time.unscaledTime + PresentationLifetimeSeconds,
            });
        }

        private static void OnTrinketAdded(
            TarotCards.Card cardType, PlayerFarming player)
        {
            PrunePresented();
            for (var index = 0; index < Presented.Count; index++)
            {
                var presented = Presented[index];
                if (presented.Card.CardType != cardType) continue;
                if (presented.Player != null && player != null && presented.Player != player) continue;

                Acquired.Add(presented.Card);
                _acquiredDueAt = Time.unscaledTime + AcquisitionSettleSeconds;
                Presented.RemoveAt(index);
                return;
            }
        }

        private static void OnTrinketRemoved(
            TarotCards.Card cardType, PlayerFarming player)
        {
            if (!Plugin.SpeechEnabled.Value) return;
            var card = new TarotCards.TarotCard(cardType, 0);
            var spoken = $"Tarot card removed: {TarotCardDescriber.CardName(card)}";
            Plugin.Log.LogInfo(
                $"[tarot] event=removed card={cardType} player={PlayerName(player)} " +
                $"spoken=\"{spoken}\"");
            Speaker.Say(spoken, SpeechPriority.Queued);
        }

        private static void PrunePresented()
        {
            for (var index = Presented.Count - 1; index >= 0; index--)
            {
                if (Time.unscaledTime >= Presented[index].ExpiresAt)
                    Presented.RemoveAt(index);
            }
        }

        private static bool SameCard(
            TarotCards.TarotCard left, TarotCards.TarotCard right) =>
            left != null && right != null &&
            left.CardType == right.CardType && left.UpgradeIndex == right.UpgradeIndex;

        private static PlayerFarming CurrentPlayer()
        {
            try
            {
                var player = MonoSingleton<UINavigatorNew>.Instance?.AllowInputOnlyFromPlayer;
                if (player != null) return player;
            }
            catch
            {
                // A presentation can start immediately before navigator ownership transfers.
            }

            return PlayerFarming.Instance;
        }

        private static string PlayerName(PlayerFarming player) =>
            player == null ? "unknown" : player.isLamb ? "Lamb" : "Goat";
    }

}
