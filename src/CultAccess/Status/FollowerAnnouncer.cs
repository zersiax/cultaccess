using System;
using CultAccess.Navigation;
using CultAccess.Speech;
using UnityEngine;

namespace CultAccess.Status
{
    /// <summary>
    /// The on-demand "tell me about this follower" key.
    ///
    /// It answers about the follower the player has already chosen in the target list, and
    /// falls back to the nearest one when the selection is something else. That ordering is
    /// the whole design: stepping the Followers filter and pressing this key is how a blind
    /// player does what a sighted player does by looking at the icons over someone's head,
    /// and it reuses a list they already know rather than inventing a second way to pick a
    /// follower.
    ///
    /// Nothing here is automatic. A base with twenty followers would talk continuously, and
    /// the proximity feed that would need a noise budget is a separate piece of work; this is
    /// the half that can be built and heard without one.
    /// </summary>
    internal static class FollowerAnnouncer
    {
        internal static void AnnounceSelected()
        {
            var follower = FromSelection() ?? Nearest(out _);
            if (follower == null)
            {
                Plugin.Log.LogInfo("[follower readout] source=none");
                Speaker.Say(Localization.Strings.Get("follower.none"));
                return;
            }

            var snapshot = FollowerReader.FromFollower(follower);
            if (snapshot == null)
            {
                Plugin.Log.LogInfo("[follower readout] source=found but unreadable");
                Speaker.Say(Localization.Strings.Get("follower.unknown"));
                return;
            }

            var text = FollowerStatusText.Detail(snapshot);
            Plugin.Log.LogInfo(
                $"[follower readout] name=\"{snapshot.Name}\" condition=\"{snapshot.Condition}\" " +
                $"need=\"{snapshot.BiggestNeed}\" task=\"{snapshot.Task}\" " +
                $"loyalty={snapshot.Loyalty} food={snapshot.Food} health={snapshot.Health} " +
                $"spoken=\"{text}\"");
            Speaker.Say(text, SpeechPriority.Now);
        }

        /// <summary>
        /// The follower behind the currently selected target, if it is one. Followers reach
        /// the catalogue as their <c>interaction_FollowerInteraction</c>, which holds a live
        /// <c>follower</c> reference the whole time — the same field the target list already
        /// takes their name from, rather than the label, which is blank at range.
        /// </summary>
        private static Follower FromSelection()
        {
            try
            {
                var selected = TargetCatalog.Selected;
                if (selected == null || !selected.Alive) return null;

                var interaction = selected.Interaction as interaction_FollowerInteraction;
                return interaction == null ? null : interaction.follower;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[follower readout] could not read the selection: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// The nearest living follower, from the game's own maintained list rather than a
        /// hierarchy search.
        /// </summary>
        private static Follower Nearest(out float distance)
        {
            distance = float.PositiveInfinity;

            try
            {
                var player = PlayerFarming.Instance;
                var followers = Follower.Followers;
                if (player == null || followers == null) return null;

                Follower best = null;
                var origin = player.transform.position;

                foreach (var follower in followers)
                {
                    if (follower == null || !follower.isActiveAndEnabled) continue;
                    if (follower.Brain?.Info == null) continue;

                    var range = Vector2.Distance(follower.transform.position, origin);
                    if (range >= distance) continue;

                    distance = range;
                    best = follower;
                }

                return best;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[follower readout] could not find a follower: {e.Message}");
                return null;
            }
        }
    }
}
