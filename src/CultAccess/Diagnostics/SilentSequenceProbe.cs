using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CultAccess.Diagnostics
{
    /// <summary>
    /// Finds scripted moments that carry meaning but no text.
    ///
    /// The game marks progress with `GameManager.OnConversationNew` plus
    /// `OnConversationNext(speaker, zoom)` — a camera hold on an object. Usually dialogue
    /// follows and the mod reads it. Sometimes none does, and what is left is an animation, a
    /// sound and a held camera: a chain breaking on the dungeon door, a location being
    /// revealed on the world map. Those are the events with nothing for a text-based reader to
    /// find, and two have already reached the player as complete silence.
    ///
    /// There are around a hundred call sites and auditing them by hand would be guesswork
    /// about which ones a player actually meets. So this measures instead: every hold is
    /// recorded, and one that passes <see cref="SilenceSeconds"/> without any dialogue text is
    /// reported as silent. A session's worth of `[silent sequence]` lines is the real list,
    /// ordered by what actually happens in play rather than by what exists in the source.
    ///
    /// Log-only. It reports what went unsaid; it does not start saying it.
    /// </summary>
    [HarmonyPatch]
    internal static class SilentSequenceProbe
    {
        /// <summary>
        /// How long a hold may run with no text before it counts as silent.
        ///
        /// Dialogue arrives within a frame or two of the hold that frames it, so this only has
        /// to outlast a load hitch. Well short of the shortest hold in the game, which is 3
        /// seconds, so a real one-liner is never mistaken for silence.
        /// </summary>
        private const float SilenceSeconds = 1.5f;

        private sealed class Hold
        {
            internal string Speaker;
            internal UnityEngine.GameObject Subject;
            internal float Zoom;
            internal float StartedAt;
            internal bool Reported;
        }

        private static readonly List<Hold> Pending = new List<Hold>();
        private static int _spoken;
        private static int _framing;

        /// <summary>
        /// The last thing reported, and when. The same beat repeats — two teleporter holds,
        /// four follower holds — and a probe whose output is mostly duplicates is as hard to
        /// read as one that is mostly noise.
        /// </summary>
        private static string _lastReported = string.Empty;
        private static float _lastReportedAt = float.NegativeInfinity;
        private static int _suppressed;

        private const float DuplicateWindowSeconds = 20f;

        internal static bool Enabled = true;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.OnConversationNext))]
        private static void AfterHold(GameObject Speaker, float Zoom)
        {
            if (!Enabled) return;

            // Measured 2026-08-25: fifteen of twenty-three holds were the camera framing the
            // Lamb's own body — skeleton bones, the player controller, the player's spine.
            // Those are cinematography, not events, and they buried the four follower holds
            // and two teleporter holds that are the actual subject. Dropped at the source so
            // the next session's list is short enough to read.
            if (IsCameraFraming(Speaker))
            {
                _framing++;
                return;
            }

            Pending.Add(new Hold
            {
                Speaker = Describe(Speaker),
                Subject = Speaker,
                Zoom = Zoom,
                StartedAt = Time.unscaledTime,
            });
        }

        /// <summary>
        /// Any text reaching the reader clears the pending holds: a hold that was framing
        /// something spoken is not the thing being hunted here.
        /// </summary>
        internal static void NoteSpokenText()
        {
            if (!Enabled || Pending.Count == 0) return;

            _spoken++;
            Pending.Clear();
        }

        /// <summary>Driven from the plugin's update; cheap when nothing is pending.</summary>
        internal static void Tick()
        {
            if (!Enabled || Pending.Count == 0) return;

            var now = Time.unscaledTime;
            for (var i = Pending.Count - 1; i >= 0; i--)
            {
                var hold = Pending[i];
                if (now - hold.StartedAt < SilenceSeconds) continue;

                Pending.RemoveAt(i);
                if (hold.Reported) continue;

                hold.Reported = true;

                var subject = Subject(hold);
                if (subject == _lastReported && now - _lastReportedAt < DuplicateWindowSeconds)
                {
                    _suppressed++;
                    continue;
                }

                _lastReported = subject;
                _lastReportedAt = now;

                Plugin.Log.LogInfo(
                    $"[silent sequence] subject=\"{subject}\" speaker=\"{hold.Speaker}\" " +
                    $"zoom={hold.Zoom:0.0} heldFor={now - hold.StartedAt:0.00}s textSeen=False " +
                    $"framingDropped={_framing} duplicatesDropped={_suppressed}");
            }
        }

        internal static void Reset()
        {
            Pending.Clear();
            _spoken = 0;
            _framing = 0;
            _suppressed = 0;
            _lastReported = string.Empty;
            _lastReportedAt = float.NegativeInfinity;
        }

        /// <summary>
        /// True for the camera framing the player rather than an event. These are the Lamb's
        /// own skeleton bones, spine and controller: the game cuts to them constantly as
        /// cinematography and none of it is a thing that went unsaid.
        /// </summary>
        private static bool IsCameraFraming(UnityEngine.GameObject speaker)
        {
            if (speaker == null) return false;
            if (speaker.GetComponent<PlayerFarming>() != null) return true;
            if (speaker.GetComponent<PlayerController>() != null) return true;

            // Bones and spines belong to whoever owns them; only the player's are framing,
            // because a bone on a follower is the game pointing at that follower.
            var bone = speaker.GetComponent<Spine.Unity.SkeletonUtilityBone>() != null ||
                       speaker.GetComponent<Spine.Unity.SkeletonAnimation>() != null;
            if (!bone) return false;

            return speaker.GetComponentInParent<PlayerFarming>() != null ||
                   speaker.GetComponentInParent<Follower>() == null;
        }

        /// <summary>
        /// What the hold was actually about, in words, for the cases worth chasing. A follower
        /// hold is the game pointing at a specific follower doing a specific thing, and the
        /// player has identified those as the bulk of what still needs describing — so the
        /// name and the current task are recorded rather than a component list.
        /// </summary>
        private static string Subject(Hold hold)
        {
            var speaker = hold.Subject;
            if (speaker == null) return "gone";

            try
            {
                var follower = speaker.GetComponent<Follower>() ??
                               speaker.GetComponentInParent<Follower>();
                if (follower != null)
                {
                    var info = follower.Brain?._directInfoAccess;
                    var name = info == null
                        ? "unnamed"
                        : Util.RichText.Clean(info.Name);
                    var task = follower.Brain == null
                        ? "unknown"
                        : follower.Brain.CurrentTaskType.ToString();
                    var state = info == null ? "unknown" : info.CursedState.ToString();
                    return $"follower:{name} task={task} cursed={state}";
                }

                var interaction = speaker.GetComponent<Interaction>() ??
                                  speaker.GetComponentInParent<Interaction>();
                if (interaction != null)
                    return $"interaction:{interaction.GetType().Name}";

                return $"object:{speaker.name}";
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[silent sequence] could not identify a hold: {e.Message}");
                return $"object:{speaker.name}";
            }
        }

        /// <summary>
        /// Name the held object by its component types as well as its name, because the object
        /// name alone is frequently "GameObject" and the component is what identifies which
        /// scripted moment this was.
        /// </summary>
        private static string Describe(GameObject speaker)
        {
            if (speaker == null) return "none";

            var behaviours = speaker.GetComponents<MonoBehaviour>();
            if (behaviours == null || behaviours.Length == 0) return speaker.name;

            var types = new List<string>(behaviours.Length);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null) continue;

                var type = behaviour.GetType().Name;
                if (!types.Contains(type)) types.Add(type);
            }

            return types.Count == 0
                ? speaker.name
                : $"{speaker.name} [{string.Join(", ", types.ToArray())}]";
        }
    }
}
