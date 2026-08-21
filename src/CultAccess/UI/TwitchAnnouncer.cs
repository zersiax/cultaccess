using System;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;
using UI.Menus.TwitchMenu;
using UnityEngine;
using UnityEngine.UI;

namespace CultAccess.UI
{
    /// <summary>
    /// Mirrors Twitch integration state that is otherwise conveyed by changing labels,
    /// progress bars, confetti, or a browser appearing outside the game. Gameplay effects
    /// that already create localized NotificationTwitch cards continue through the generic
    /// NotificationAnnouncer, avoiding a second reading of the same consequence.
    /// </summary>
    internal static class TwitchAnnouncer
    {
        private static bool _initialized;
        private static bool _loginPending;
        private static bool _loginReminderSpoken;
        private static float _loginRequestedAt;
        private static bool _lastSocketConnected;

        private static int _lastTotemContributions = -1;
        private static int _lastRaffleParticipants = -1;
        private static int _pendingRaffleParticipants = -1;
        private static float _raffleAnnouncementAt;
        private static int _lastVoteCount = -1;
        private static int _pendingVoteCount = -1;
        private static float _voteAnnouncementAt;

        internal static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            TwitchAuthentication.OnAuthenticated += OnAuthenticated;
            TwitchAuthentication.OnLoggedOut += OnLoggedOut;
            TwitchTotem.TotemUpdated += OnTotemUpdated;
            TwitchFollowers.RaffleUpdated += OnRaffleUpdated;
            TwitchVoting.OnVotingUpdated += OnVotingUpdated;

            _lastSocketConnected = TwitchRequest.SocketConnected;
        }

        internal static void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;

            TwitchAuthentication.OnAuthenticated -= OnAuthenticated;
            TwitchAuthentication.OnLoggedOut -= OnLoggedOut;
            TwitchTotem.TotemUpdated -= OnTotemUpdated;
            TwitchFollowers.RaffleUpdated -= OnRaffleUpdated;
            TwitchVoting.OnVotingUpdated -= OnVotingUpdated;
        }

        internal static void Tick()
        {
            if (!_initialized) return;

            var socketConnected = TwitchRequest.SocketConnected;
            if (TwitchAuthentication.IsAuthenticated && socketConnected != _lastSocketConnected)
            {
                if (socketConnected)
                    AnnounceReady();
                else
                    Say("Twitch realtime connection lost. Channel point events are paused.",
                        SpeechPriority.Queued);
            }
            _lastSocketConnected = socketConnected;

            if (_loginPending && !_loginReminderSpoken &&
                Time.unscaledTime >= _loginRequestedAt + 60f)
            {
                _loginReminderSpoken = true;
                Say("Still waiting for Twitch. Complete authorization in the browser, and make sure " +
                    "the channel is live with Cult of the Lamb selected.", SpeechPriority.Queued);
            }

            if (_pendingRaffleParticipants >= 0 && Time.unscaledTime >= _raffleAnnouncementAt)
            {
                var count = _pendingRaffleParticipants;
                _pendingRaffleParticipants = -1;
                Say($"Twitch follower raffle, {count} {(count == 1 ? "participant" : "participants")}.",
                    SpeechPriority.Queued);
            }

            if (_pendingVoteCount >= 0 && Time.unscaledTime >= _voteAnnouncementAt)
            {
                var count = _pendingVoteCount;
                _pendingVoteCount = -1;
                Say($"Twitch vote count, {count}.", SpeechPriority.Queued);
            }
        }

        internal static bool TryDescribeSettingsContext(Selectable selectable, out string description)
        {
            description = null;
            if (selectable == null ||
                selectable.GetComponentInParent<UITwitchSettingsMenuController>() == null)
                return false;

            string state;
            if (TwitchAuthentication.IsAuthenticated)
            {
                var channel = ChannelName();
                state = TwitchRequest.SocketConnected
                    ? $"Connected as {channel}; realtime integration ready"
                    : $"Authorized as {channel}; realtime connection starting";
            }
            else if (!string.IsNullOrEmpty(TwitchManager.Token))
            {
                state = "Authorization saved, but integration inactive. The channel must be live " +
                        "with Cult of the Lamb selected; return to the game to retry";
            }
            else
            {
                state = "Not connected. Connect opens browser authorization. The channel must be " +
                        "live with Cult of the Lamb selected for integration to activate";
            }

            description = "Twitch settings. " + state +
                          ". Integration configuration opens the Twitch extension dashboard";
            return true;
        }

        internal static void OnLoginRequested(ref Action<TwitchRequest.ResponseType> callback)
        {
            _loginPending = true;
            _loginReminderSpoken = false;
            _loginRequestedAt = Time.unscaledTime;

            var original = callback;
            callback = response =>
            {
                original?.Invoke(response);
                OnLoginResponse(response);
            };

            Log("authorization requested");
            Say("Starting Twitch authorization. Complete it in the browser that opens. The channel " +
                "must be live with Cult of the Lamb selected before integration becomes active.");
        }

        internal static void AnnounceExtensionDashboard()
        {
            Log("opening extension dashboard");
            Say("Opening the Cult of the Lamb Twitch extension dashboard in your browser. " +
                "Activate and configure the extension there.");
        }

        internal static void OnRaffleState(UIMenuBase menu, string state)
        {
            Log($"follower raffle overlay state={state}");
            if (state == "Pre" || state == "Active")
            {
                _lastRaffleParticipants = 0;
                _pendingRaffleParticipants = -1;
            }

            PanelReader.ReadSoon(menu);
        }

        internal static void OnVotingState(UIMenuBase menu, string state)
        {
            Log($"follower voting overlay state={state}");
            if (state == "Voting")
            {
                _lastVoteCount = 0;
                _pendingVoteCount = -1;
            }

            PanelReader.ReadSoon(menu);
        }

        internal static void AnnounceTotemWheelResult(UIRandomWheel wheel)
        {
            var reward = wheel?.ChosenSegment;
            if (reward == null) return;

            string name;
            switch (reward.reward)
            {
                case InventoryItem.ITEM_TYPE.FOUND_ITEM_DECORATION:
                    name = "an exclusive decoration or follower form";
                    break;
                case InventoryItem.ITEM_TYPE.FOLLOWERS:
                    name = "a new follower";
                    break;
                default:
                    var localizedName = InventoryItem.LocalizedName(reward.reward);
                    name = RichText.IsUsableLocalization(
                        localizedName, $"Inventory/{reward.reward}")
                        ? RichText.Clean(localizedName)
                        : RichText.Humanise(reward.reward.ToString());
                    break;
            }

            Log($"totem wheel result={reward.reward}");
            Say($"Twitch Totem reward: {name}.");
        }

        private static void OnAuthenticated()
        {
            _loginPending = false;
            _loginReminderSpoken = false;
            _lastSocketConnected = TwitchRequest.SocketConnected;

            var channel = ChannelName();
            if (_lastSocketConnected)
            {
                Log($"authenticated channel={channel} socket=connected");
                Say($"Twitch integration ready for {channel}.");
            }
            else
            {
                Log($"authenticated channel={channel} socket=connecting");
                Say($"Twitch authorized as {channel}. Connecting to the realtime service.");
            }
        }

        private static void AnnounceReady()
        {
            var channel = ChannelName();
            Log($"socket connected channel={channel}");
            Say($"Twitch integration ready for {channel}.");
        }

        private static void OnLoggedOut()
        {
            _loginPending = false;
            _loginReminderSpoken = false;
            _lastSocketConnected = false;
            Log("signed out");
            Say("Signed out of Twitch.");
        }

        private static void OnLoginResponse(TwitchRequest.ResponseType response)
        {
            if (TwitchAuthentication.IsAuthenticated) return;

            _loginPending = false;
            _loginReminderSpoken = false;

            if (!string.IsNullOrEmpty(TwitchManager.Token))
            {
                Log($"authorization saved but inactive response={response}");
                Say("Twitch authorization was saved, but integration is not active. Make sure the " +
                    "channel is live with Cult of the Lamb selected, then return to the game.");
            }
            else
            {
                Log($"authorization failed response={response}");
                Say("Twitch authorization did not complete. Check the browser and network connection, " +
                    "then try Connect again.");
            }
        }

        private static void OnTotemUpdated(int contributions)
        {
            Log($"totem contributions={contributions}/10");
            if (contributions >= TwitchTotem.ContributionTarget &&
                (_lastTotemContributions < TwitchTotem.ContributionTarget))
            {
                Say("Twitch Totem complete. A reward is ready at the shrine.", SpeechPriority.Queued);
            }
            _lastTotemContributions = contributions;
        }

        private static void OnRaffleUpdated(
            TwitchRequest.ResponseType response, TwitchFollowers.RaffleData data)
        {
            if (response != TwitchRequest.ResponseType.Success || data == null) return;
            if (!IsActiveMenu<UITwitchFollowerSelectOverlayController>()) return;
            if (data.participants == _lastRaffleParticipants) return;

            _lastRaffleParticipants = data.participants;
            _pendingRaffleParticipants = data.participants;
            _raffleAnnouncementAt = Time.unscaledTime + 0.35f;
            Log($"raffle participants={data.participants}");
        }

        private static void OnVotingUpdated(int totalVotes)
        {
            if (!IsActiveMenu<src.UI.Overlays.TwitchFollowerVotingOverlay.UITwitchFollowerVotingOverlayController>())
                return;
            if (totalVotes == _lastVoteCount) return;

            _lastVoteCount = totalVotes;
            _pendingVoteCount = totalVotes;
            _voteAnnouncementAt = Time.unscaledTime + 0.35f;
            Log($"follower vote count={totalVotes}");
        }

        private static bool IsActiveMenu<T>() where T : UIMenuBase
        {
            foreach (var menu in UIMenuBase.ActiveMenus)
            {
                if (menu is T && menu.gameObject.activeInHierarchy) return true;
            }
            return false;
        }

        private static string ChannelName()
        {
            var channel = RichText.Clean(TwitchManager.ChannelName);
            return channel.Length > 0 ? channel : "your channel";
        }

        private static void Say(string text, SpeechPriority priority = SpeechPriority.Now)
        {
            if (Plugin.SpeechEnabled.Value) Speaker.Say(text, priority);
        }

        private static void Log(string message)
        {
            Plugin.Log.LogInfo($"[twitch] {message}");
        }
    }

    [HarmonyPatch]
    internal static class TwitchPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TwitchAuthentication), nameof(TwitchAuthentication.RequestLogIn))]
        private static void BeforeRequestLogIn(ref Action<TwitchRequest.ResponseType> callback)
        {
            TwitchAnnouncer.OnLoginRequested(ref callback);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UITwitchSettingsMenuController), "OnIntegrationConfigurationButtonClicked")]
        private static void BeforeSettingsExtensionDashboard()
        {
            TwitchAnnouncer.AnnounceExtensionDashboard();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UITwitchExtensionButton), "OnClick")]
        private static void BeforeExtensionDashboard()
        {
            TwitchAnnouncer.AnnounceExtensionDashboard();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UITwitchFollowerSelectOverlayController), "SetState")]
        private static void AfterRaffleState(
            UITwitchFollowerSelectOverlayController __instance, object[] __args)
        {
            TwitchAnnouncer.OnRaffleState(__instance, StateName(__args));
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(src.UI.Overlays.TwitchFollowerVotingOverlay.UITwitchFollowerVotingOverlayController),
            "SetState")]
        private static void AfterVotingState(
            src.UI.Overlays.TwitchFollowerVotingOverlay.UITwitchFollowerVotingOverlayController __instance,
            object[] __args)
        {
            TwitchAnnouncer.OnVotingState(__instance, StateName(__args));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIRandomWheel), "OnHideCompleted")]
        private static void BeforeRandomWheelHidden(UIRandomWheel __instance)
        {
            if (__instance is UITwitchTotemWheel)
                TwitchAnnouncer.AnnounceTotemWheelResult(__instance);
        }

        private static string StateName(object[] arguments)
        {
            return arguments != null && arguments.Length > 0 && arguments[0] != null
                ? arguments[0].ToString()
                : "Unknown";
        }
    }
}
