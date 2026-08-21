using System.Collections.Generic;
using CultAccess.Navigation;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using TMPro;

namespace CultAccess.UI
{
    /// <summary>
    /// Announces the interaction prompt — "Kneel to be Sacrificed", "Harvest", "Talk" —
    /// that appears when the player is standing close enough to use something.
    ///
    /// Hooked on the Indicator's own <c>Update</c>, and that took two attempts.
    /// <c>Interaction.OnBecomeCurrent</c> is <c>public virtual</c> across dozens of
    /// subclasses, so overrides skipping base would silently never announce.
    /// <c>Indicator.Reset</c> looked like the shared choke point, but it is a layout pass
    /// the game does not run on every prompt appearance, so prompts were missed. Watching
    /// the rendered text each frame catches every assignment path regardless.
    ///
    /// Announced only when the text changes — otherwise standing next to a bush would
    /// repeat "Harvest" every frame. A per-frame string comparison is negligible.
    /// </summary>
    [HarmonyPatch]
    internal static class InteractionPromptAnnouncer
    {
        /// <summary>
        /// How long a changing prompt must hold still before the new value is spoken.
        ///
        /// Holding Interact on a resource pile rewrites the prompt on every tick of the
        /// harvest — fifty left, forty-nine, forty-eight — and each rewrite was its own
        /// queued announcement. A queue drops nothing, so the player heard the whole
        /// countdown long after the pile was empty. Only the number they end on is worth
        /// hearing, and this is the same settle window the rest of the mod uses for bursts.
        /// </summary>
        private const float SettleSeconds = 0.4f;

        private struct Pending
        {
            internal string Text;
            internal float DueAt;
        }

        private static readonly Dictionary<int, string> LastPromptByIndicator = new Dictionary<int, string>();
        private static readonly Dictionary<int, int> LastInteractionByIndicator = new Dictionary<int, int>();
        private static readonly Dictionary<int, Pending> PendingByIndicator = new Dictionary<int, Pending>();
        private static readonly List<int> Due = new List<int>();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Indicator), "Update")]
        private static void AfterUpdate(Indicator __instance)
        {
            if (!Plugin.SpeechEnabled.Value || !Plugin.AnnounceInteractionPrompts.Value) return;
            if (__instance == null) return;

            var renderedPrompt = Combine(
                __instance.text,
                __instance.SecondaryText,
                __instance.Thirdtext,
                __instance.Fourthtext);

            var id = __instance.GetInstanceID();

            if (renderedPrompt == null)
            {
                // Prompt cleared: forget it so walking back up to the same thing speaks
                // again, and drop anything still waiting to be said about it. A settled
                // count for a pile the player has already walked away from is stale.
                Forget(id);
                return;
            }

            var current = __instance.playerFarming?.interactor?.CurrentInteraction;
            var dungeonDoor = current as Interaction_BaseDungeonDoor;
            var weaponPodium = current as Interaction_WeaponSelectionPodium;
            var heartPickup = current as Interaction_HeartPickupBase;
            var prompt = dungeonDoor != null
                ? DungeonDoorPassage.Prompt(dungeonDoor)
                : weaponPodium != null
                    ? WeaponPodiumTarget.Prompt(weaponPodium, __instance.playerFarming)
                    : heartPickup != null
                        ? HeartPickupTarget.Name(heartPickup)
                        : renderedPrompt;
            if (!RichText.HasSemanticText(prompt))
            {
                Forget(id);
                return;
            }
            // Use the semantic prompt in the key. A podium can reroll to a different item
            // while its rendered label remains the generic word "Equip".
            var sourceKey = $"{(current == null ? 0 : current.GetInstanceID())}:{prompt}";
            if (LastPromptByIndicator.TryGetValue(id, out var previous) && previous == sourceKey)
                return;
            LastPromptByIndicator[id] = sourceKey;

            // Whether the button must be held is not discoverable without sight: the game
            // shows a radial fill instead of saying so, and a tap silently does nothing.
            var text = RequiresHold(__instance) ? $"{prompt}, hold to activate" : prompt;

            // The prompt is whatever the game's own indicator says, and the game's data is not
            // always finished: a base load once spoke "asdasdasdasdasdasdasdasddfdsfdsfdsf",
            // which is developer keyboard-mashing left on some object's label. Nothing here can
            // tell that from a real word, and refusing to speak anything odd-looking would risk
            // silencing legitimate names. What was missing was the ability to say *which*
            // object, so the prompt is logged with the interaction that produced it.
            if (Plugin.LogNavigation.Value)
            {
                // Both interactions are reported, because CurrentInteraction is null for most
                // prompts: the indicator renders a candidate before the interactor commits to
                // one. The first version of this logged only CurrentInteraction, concluded
                // "the placeholder has no interaction", and would have justified suppressing
                // null-interaction prompts — which the same log shows would have silenced
                // "Mine Stone", "Travel", "Speak to <follower>" and about sixty others.
                Plugin.Log.LogInfo(
                    $"[interaction prompt] current={Identify(current)} " +
                    $"temp={Identify(__instance.playerFarming?.interactor?.TempInteraction)} " +
                    $"indicator=\"{__instance.gameObject.name}\" " +
                    $"prompt=\"{prompt}\" hold={RequiresHold(__instance)}");
            }

            // A different interaction is a new thing to know about, and is spoken at once.
            // The same interaction rewriting its own prompt is a value changing under the
            // player's thumb, and only where it settles is worth hearing.
            var interactionId = current == null ? 0 : current.GetInstanceID();
            var known = LastInteractionByIndicator.TryGetValue(id, out var previousInteraction);
            LastInteractionByIndicator[id] = interactionId;

            if (!known || previousInteraction != interactionId)
            {
                PendingByIndicator.Remove(id);

                // Queued, not interrupting. Walking up to something usually coincides with
                // arrival announcements and other movement feedback, and whichever spoke
                // second was destroying the first. Neither is urgent enough to talk over
                // the other.
                Speaker.Say(text, SpeechPriority.Queued);
                return;
            }

            PendingByIndicator[id] = new Pending
            {
                Text = text,
                DueAt = UnityEngine.Time.unscaledTime + SettleSeconds,
            };
        }

        /// <summary>Type and object name of an interaction, or a marker when there is none.</summary>
        private static string Identify(Interaction interaction)
        {
            if (interaction == null) return "<none>";

            try
            {
                return $"{interaction.GetType().Name}:\"{interaction.gameObject.name}\"";
            }
            catch (System.Exception)
            {
                return "<destroyed>";
            }
        }

        /// <summary>Speak any prompt that has now held still. Driven from the plugin's update.</summary>
        internal static void Tick()
        {
            if (PendingByIndicator.Count == 0) return;

            var now = UnityEngine.Time.unscaledTime;
            Due.Clear();

            foreach (var pair in PendingByIndicator)
                if (now >= pair.Value.DueAt)
                    Due.Add(pair.Key);

            foreach (var id in Due)
            {
                var text = PendingByIndicator[id].Text;
                PendingByIndicator.Remove(id);
                Speaker.Say(text, SpeechPriority.Queued);
            }
        }

        internal static void Reset()
        {
            LastPromptByIndicator.Clear();
            LastInteractionByIndicator.Clear();
            PendingByIndicator.Clear();
        }

        private static void Forget(int id)
        {
            LastPromptByIndicator.Remove(id);
            LastInteractionByIndicator.Remove(id);
            PendingByIndicator.Remove(id);
        }

        /// <summary>
        /// Mirrors the game's own condition in <c>Indicator.Reset</c>: a hold is required only
        /// when the interaction asks for one *and* the hold-actions accessibility setting is on.
        /// </summary>
        private static bool RequiresHold(Indicator indicator)
        {
            try
            {
                if (!indicator.HoldToInteract) return false;
                return SettingsManager.Settings.Accessibility.HoldActions;
            }
            catch
            {
                // Settings may not be loaded; fall back to the interaction's own intent.
                return indicator.HoldToInteract;
            }
        }

        private static string Combine(params TextMeshProUGUI[] labels)
        {
            var parts = new List<string>(labels.Length);

            foreach (var label in labels)
            {
                if (label == null) continue;

                var clean = RichText.Clean(label.text);
                if (clean.Length == 0) continue;
                if (parts.Contains(clean)) continue;

                parts.Add(clean);
            }

            return parts.Count == 0 ? null : string.Join(", ", parts.ToArray());
        }
    }
}
