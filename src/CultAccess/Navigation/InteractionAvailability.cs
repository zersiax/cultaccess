using System.Collections.Generic;
using CultAccess.Util;

namespace CultAccess.Navigation
{
    /// <summary>
    /// A snapshot of the gates the game's <see cref="Interactor"/> applies before an
    /// interaction can respond. This deliberately does not call an interaction "locked":
    /// the base interaction API exposes availability, but no general lock state.
    /// </summary>
    internal sealed class InteractionAvailability
    {
        private static readonly HashSet<string> ReportedReadFailures = new HashSet<string>();

        public bool ObjectActive { get; private set; }
        public bool ComponentEnabled { get; private set; }
        public bool Registered { get; private set; }
        public bool HasSelectableLabel { get; private set; }
        public bool Primary { get; private set; }
        public bool Secondary { get; private set; }
        public bool Third { get; private set; }
        public bool Fourth { get; private set; }
        public string PrimaryLabel { get; private set; }
        public string SecondaryLabel { get; private set; }
        public string ThirdLabel { get; private set; }
        public string FourthLabel { get; private set; }

        public bool HasAction => Primary || Secondary || Third || Fourth;

        /// <summary>
        /// True when this interaction has no live label solely because the game is holding a
        /// story beat. The target is not broken and not permanently locked: the whole world is
        /// unusable until the beat ends. Distinguishing this from ordinary unavailability is
        /// what stops an empty action list reading as a softlock.
        /// </summary>
        public bool StoryGateSuppressed { get; private set; }

        /// <summary>
        /// True only when the component is in the game's live registry, can be selected,
        /// and exposes at least one action the Interactor will execute.
        /// </summary>
        public bool Available =>
            ObjectActive && ComponentEnabled && Registered && HasSelectableLabel && HasAction;

        public string FirstLabel
        {
            get
            {
                if (PrimaryLabel.Length > 0) return PrimaryLabel;
                if (SecondaryLabel.Length > 0) return SecondaryLabel;
                if (ThirdLabel.Length > 0) return ThirdLabel;
                return FourthLabel.Length > 0 ? FourthLabel : null;
            }
        }

        /// <summary>Short player-facing state appended to a navigation target.</summary>
        public string SpeechStatus
        {
            get
            {
                // Say why, not just that. "Currently unavailable" on every object in the base
                // at once is what made a story beat feel like a broken save.
                if (StoryGateSuppressed)
                    return "not usable during this story moment";
                if (!Available) return "currently unavailable";
                if (Primary) return null;

                var actions = new List<string>();
                if (Secondary) actions.Add("Interact 2");
                if (Third) actions.Add("Interact 3");
                if (Fourth) actions.Add("Interact 4");
                return actions.Count == 0
                    ? "currently unavailable"
                    : string.Join(" or ", actions.ToArray()) + " only";
            }
        }

        public string ActionsForLog
        {
            get
            {
                var actions = new List<string>();
                if (Primary) actions.Add("primary");
                if (Secondary) actions.Add("secondary");
                if (Third) actions.Add("third");
                if (Fourth) actions.Add("fourth");
                return actions.Count == 0 ? "none" : string.Join(",", actions.ToArray());
            }
        }

        public string UnavailableReason
        {
            get
            {
                if (!ObjectActive) return "object inactive";
                if (!ComponentEnabled) return "component disabled";
                if (!Registered) return "not in Interaction.interactions";
                if (StoryGateSuppressed) return "story beat suppressing all labels";
                if (!HasSelectableLabel) return "no live label";
                if (!HasAction) return "all interaction actions disabled";
                return null;
            }
        }

        public static InteractionAvailability Inspect(Interaction interaction)
        {
            var result = new InteractionAvailability
            {
                PrimaryLabel = string.Empty,
                SecondaryLabel = string.Empty,
                ThirdLabel = string.Empty,
                FourthLabel = string.Empty,
            };

            if (interaction == null) return result;

            result.ObjectActive = interaction.gameObject.activeInHierarchy;
            result.ComponentEnabled = interaction.enabled;
            result.Registered = Interaction.interactions != null &&
                                Interaction.interactions.Contains(interaction);

            var typeName = interaction.GetType().Name;
            result.PrimaryLabel = ReadLabel(typeName + ".Label", () => interaction.Label);
            result.SecondaryLabel = ReadLabel(typeName + ".SecondaryLabel", () => interaction.SecondaryLabel);
            result.ThirdLabel = ReadLabel(typeName + ".ThirdLabel", () => interaction.ThirdLabel);
            result.FourthLabel = ReadLabel(typeName + ".FourthLabel", () => interaction.FourthLabel);

            var allowsNoLabel = false;
            try
            {
                allowsNoLabel = interaction.AllowInteractionWithoutLabel;
            }
            catch (System.Exception e)
            {
                LogReadFailure(
                    $"Availability check could not read AllowInteractionWithoutLabel on " +
                    $"{typeName}: {e.Message}");
            }

            // This is the exact label gate in Interactor.GetClosestInteraction.
            result.HasSelectableLabel = allowsNoLabel ||
                                        result.PrimaryLabel.Length > 0 ||
                                        result.SecondaryLabel.Length > 0 ||
                                        result.ThirdLabel.Length > 0 ||
                                        result.FourthLabel.Length > 0;

            // Only meaningful when the label is what failed; an interaction the game would
            // refuse anyway is not excused by the story gate.
            result.StoryGateSuppressed = !result.HasSelectableLabel &&
                                         StoryLabelGate.Suppresses(interaction);

            // Once selected, these are the exact per-input gates in Interactor.Update.
            result.Primary = interaction.Interactable;
            result.Secondary = interaction.HasSecondaryInteraction && interaction.SecondaryInteractable;
            result.Third = interaction.HasThirdInteraction && interaction.ThirdInteractable;
            result.Fourth = interaction.HasFourthInteraction && interaction.FourthInteractable;
            return result;
        }

        private static string ReadLabel(string source, System.Func<string> getter)
        {
            try
            {
                return RichText.Clean(getter());
            }
            catch (System.Exception e)
            {
                // Label getters consult live quest/tutorial state. A failed getter is useful
                // diagnostic evidence and must not silently turn into an asserted lock state.
                LogReadFailure($"Interaction label read failed for {source}: {e.Message}");
                return string.Empty;
            }
        }

        private static void LogReadFailure(string message)
        {
            if (ReportedReadFailures.Add(message)) Plugin.Log.LogWarning(message);
        }
    }
}
