using System;
using System.Collections.Generic;
using CultAccess.Navigation;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using UnityEngine;

namespace CultAccess.Status
{
    /// <summary>
    /// Reads the speech bubbles followers put over their heads.
    ///
    /// This is the game's own push channel for "a follower needs you", and it was reaching the
    /// player as a bare sound. <c>WorshipperBubble.Play</c> shows one of twenty-one icons for
    /// four seconds and plays <c>event:/followers/speech_bubble</c> positioned at the
    /// follower — so a blind player already hears that something was said, from roughly the
    /// right direction, and gets none of what it was. Announcing something and withholding its
    /// content is worse than saying nothing.
    ///
    /// The icon is only half the answer. A follower who has crossed the base to find the
    /// player is running <c>FollowerTask_GetAttention</c>, which carries a typed
    /// <c>ComplaintType</c> — hungry, homeless, ill, ready to level up, holding a finished
    /// quest — while the bubble shows a generic help icon for all of them. Where that task is
    /// running, its reason wins over the icon.
    ///
    /// Hooking the bubble rather than the task means the game supplies the pacing. It repeats
    /// every four to six seconds for as long as the follower is waiting, which is far too
    /// often to speak, so a per-follower cooldown reduces that to one line until either the
    /// reason changes or the player has had time to act.
    /// </summary>
    [HarmonyPatch]
    internal static class FollowerAttentionAnnouncer
    {
        /// <summary>
        /// How long the same follower saying the same thing stays quiet. The game re-bubbles
        /// every four to six seconds indefinitely, so without this a single unhoused follower
        /// would talk over everything else for the rest of the session. Long enough to cross
        /// the base and deal with them; short enough that an ignored one is a reminder rather
        /// than a single missable line.
        /// </summary>
        private const float RepeatCooldownSeconds = 45f;

        /// <summary>
        /// Bubbles fire wherever a follower is, including behind buildings and across the
        /// whole base. The scan radius is the player's own already-tunable idea of "near me",
        /// so it decides this too rather than introducing a second number that means the same
        /// thing.
        /// </summary>
        private static float Range => Plugin.ScanRadius.Value;

        private sealed class Spoken
        {
            public string Reason;
            public float At;
        }

        private static readonly Dictionary<int, Spoken> LastSpoken = new Dictionary<int, Spoken>();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorshipperBubble), nameof(WorshipperBubble.Play))]
        private static void AfterBubblePlayed(
            WorshipperBubble __instance, WorshipperBubble.SPEECH_TYPE Type)
        {
            if (__instance == null) return;
            if (!Plugin.SpeechEnabled.Value || !Plugin.AnnounceFollowerRequests.Value) return;

            try
            {
                Announce(__instance, Type);
            }
            catch (Exception e)
            {
                // A bubble is fired from several follower tasks and from onboarding; none of
                // them should be able to take a frame down because we could not read one.
                Plugin.Log.LogWarning($"[follower request] could not read a bubble: {e.Message}");
            }
        }

        private static void Announce(WorshipperBubble bubble, WorshipperBubble.SPEECH_TYPE type)
        {
            var follower = Owner(bubble);
            var info = follower?.Brain?._directInfoAccess;
            if (info == null)
            {
                Plugin.Log.LogInfo($"[follower request] type={type} owner=unresolved");
                return;
            }

            // The task's own reason where there is one; the icon otherwise. A follower who has
            // walked over to ask for a house shows the same help bubble as one asking for
            // anything else, so the icon alone would lose the entire distinction.
            var attention = follower.Brain.CurrentTask as FollowerTask_GetAttention;
            var reason = FollowerReader.AttentionReason(attention);
            var source = "task";
            if (reason.Length == 0)
            {
                reason = BubbleReason(type);
                source = "bubble";
            }

            if (reason.Length == 0)
            {
                Plugin.Log.LogInfo($"[follower request] type={type} reason=none");
                return;
            }

            var player = NavigatorPlayer.Resolve();
            var distance = player == null
                ? -1f
                : Vector2.Distance(player.position, follower.transform.position);

            if (distance >= 0f && distance > Range)
            {
                Plugin.Log.LogInfo(
                    $"[follower request] type={type} reason=\"{reason}\" " +
                    $"distance={distance:0.#} suppressed=out-of-range");
                return;
            }

            if (!DueToSpeak(info.ID, reason))
            {
                Plugin.Log.LogInfo(
                    $"[follower request] type={type} reason=\"{reason}\" suppressed=repeat");
                return;
            }

            var name = RichText.Clean(info.Name);
            var text = player != null &&
                       Navigation.Compass.TryDescribe(
                           player.position, follower.transform.position,
                           out var bearing, out var range)
                ? Localization.Strings.Format(
                    "follower.request_located",
                    name, reason, Navigation.Compass.DescribeDistance(range), bearing)
                : Localization.Strings.Format("follower.request", name, reason);

            Plugin.Log.LogInfo(
                $"[follower request] type={type} source={source} name=\"{name}\" " +
                $"reason=\"{reason}\" distance={distance:0.#} spoken=\"{text}\"");
            Speaker.Say(text, SpeechPriority.Queued);
        }

        /// <summary>
        /// True when this follower has not already said this within the cooldown. A different
        /// reason from the same follower always speaks: going from "hungry" to "ill" is news
        /// even a second later.
        /// </summary>
        private static bool DueToSpeak(int followerId, string reason)
        {
            var now = Time.unscaledTime;
            if (LastSpoken.TryGetValue(followerId, out var previous))
            {
                if (previous.Reason == reason && now - previous.At < RepeatCooldownSeconds)
                    return false;

                previous.Reason = reason;
                previous.At = now;
                return true;
            }

            LastSpoken[followerId] = new Spoken { Reason = reason, At = now };
            return true;
        }

        /// <summary>
        /// The follower this bubble belongs to. <c>WorshipperBubble</c> keeps a reference to a
        /// <c>Worshipper</c>, which a <c>Follower</c> is not, so the hierarchy is walked
        /// instead. The registry sweep behind it is a bounded list of the followers in the
        /// scene and only runs when the walk fails, which would mean the bubble is not
        /// parented under its follower.
        /// </summary>
        private static Follower Owner(WorshipperBubble bubble)
        {
            var direct = bubble.GetComponentInParent<Follower>();
            if (direct != null) return direct;

            var followers = Follower.Followers;
            if (followers == null) return null;

            foreach (var follower in followers)
                if (follower != null && ReferenceEquals(follower.WorshipperBubble, bubble))
                    return follower;

            return null;
        }

        /// <summary>
        /// What the icon itself means. The game has no text for any of these — they are
        /// sprites in a dictionary — so the words are the mod's own. Several types are
        /// deliberately silent: the boss-crown and friends variants are ambient chatter that
        /// changes no decision, and a cue that conveys nothing new is noise even when accurate.
        /// </summary>
        private static string BubbleReason(WorshipperBubble.SPEECH_TYPE type)
        {
            switch (type)
            {
                case WorshipperBubble.SPEECH_TYPE.FOOD:
                    return Localization.Strings.Get("follower.bubble_food");
                case WorshipperBubble.SPEECH_TYPE.HOME:
                    return Localization.Strings.Get("follower.bubble_home");
                case WorshipperBubble.SPEECH_TYPE.HELP:
                    return Localization.Strings.Get("follower.bubble_help");
                case WorshipperBubble.SPEECH_TYPE.STARVING:
                    return Localization.Strings.Get("follower.bubble_starving");
                case WorshipperBubble.SPEECH_TYPE.ILL:
                    return Localization.Strings.Get("follower.bubble_ill");
                case WorshipperBubble.SPEECH_TYPE.SIN:
                    return Localization.Strings.Get("follower.bubble_sin");
                case WorshipperBubble.SPEECH_TYPE.READY:
                    return Localization.Strings.Get("follower.bubble_ready");
                case WorshipperBubble.SPEECH_TYPE.TWITCH:
                    return Localization.Strings.Get("follower.bubble_twitch");
                case WorshipperBubble.SPEECH_TYPE.DISSENTER1:
                case WorshipperBubble.SPEECH_TYPE.DISSENTER2:
                case WorshipperBubble.SPEECH_TYPE.DISSENTER3:
                    return Localization.Strings.Get("follower.bubble_dissent");
                case WorshipperBubble.SPEECH_TYPE.DISSENTARGUE:
                    return Localization.Strings.Get("follower.bubble_dissent_argue");
                case WorshipperBubble.SPEECH_TYPE.FOLLOWERMEAT:
                    return Localization.Strings.Get("follower.bubble_meat");
                default:
                    // LOVE, ENEMIES, FRIENDS and the four BOSSCROWN variants are flavour: two
                    // followers gossiping, or one admiring the Crown. Nothing to act on.
                    return string.Empty;
            }
        }

        internal static void Shutdown() => LastSpoken.Clear();
    }
}
