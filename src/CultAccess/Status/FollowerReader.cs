using System;
using CultAccess.Util;
using HarmonyLib;
using I2.Loc;
using Lamb.UI.FollowerSelect;
using UnityEngine;
using UnityEngine.UI;

namespace CultAccess.Status
{
    /// <summary>
    /// Builds a <see cref="FollowerSnapshot"/> from a live follower, so the follower card
    /// adapter and the on-demand follower key describe the same follower the same way.
    ///
    /// The bars are the point of this class. <c>FollowerInformationBox</c> draws loyalty,
    /// food, illness and pleasure as <c>Image.fillAmount</c> with a threshold colour and no
    /// text anywhere, and hides several of them depending on the follower and the screen —
    /// a dead follower loses food and illness, a mutated one loses loyalty, pleasure appears
    /// only where the caller asked for it. Rather than re-deriving those rules, this reads the
    /// card's own fills and its own container flags where a card exists, which cannot drift
    /// from what is on screen. Only when there is no card, as when the player asks about a
    /// follower standing in the world, are the same values computed from the follower's own
    /// fields.
    /// </summary>
    internal static class FollowerReader
    {
        /// <summary>Matches <c>FollowerInformationBox</c>'s own hunger denominator.</summary>
        private const float StarvationRange = 75f;
        private const float FoodRange = 175f;
        private const float PleasureRange = 65f;

        private static readonly AccessTools.FieldRef<FollowerInformationBox, Image> AdorationLevelRef =
            SafeField<FollowerInformationBox, Image>("_adorationLevel");
        private static readonly AccessTools.FieldRef<FollowerInformationBox, Image> PleasureLevelRef =
            SafeField<FollowerInformationBox, Image>("_pleasureLevel");
        private static readonly AccessTools.FieldRef<FollowerInformationBox, GameObject> AdorationContainerRef =
            SafeField<FollowerInformationBox, GameObject>("_adorationContainer");
        private static readonly AccessTools.FieldRef<FollowerInformationBox, GameObject> PleasureContainerRef =
            SafeField<FollowerInformationBox, GameObject>("_pleasureContainer");

        /// <summary>A follower tile on any picker or roster screen.</summary>
        internal static FollowerSnapshot FromCard(FollowerSelectItem item)
        {
            if (item == null) return null;

            var info = item.FollowerInfo;
            if (info == null) return null;

            var snapshot = FromInfo(info, includeTask: false);
            if (snapshot == null) return null;

            snapshot.Unavailable = UnavailableReason(item.FollowerSelectEntry);

            if (item is FollowerInformationBox box)
            {
                ReadCardFills(box, snapshot);
            }
            else
            {
                // Only FollowerInformationBox has a pleasure bar. On any other card the
                // shrine being built is not a reason to read a number that is not on screen.
                snapshot.PleasureShown = false;
            }

            return snapshot;
        }

        /// <summary>A follower standing in the world, with what they are currently doing.</summary>
        internal static FollowerSnapshot FromFollower(Follower follower)
        {
            // _directInfoAccess rather than Brain.Info: the latter is a FollowerBrainInfo
            // wrapper, and the raw FollowerInfo underneath it is what the cards are built
            // from, so reading the same object keeps both paths reporting the same numbers.
            var info = follower?.Brain?._directInfoAccess;
            return info == null ? null : FromInfo(info, includeTask: true);
        }

        internal static FollowerSnapshot FromInfo(FollowerInfo info, bool includeTask)
        {
            if (info == null) return null;

            try
            {
                var brain = LiveBrain(info.ID);
                var snapshot = new FollowerSnapshot
                {
                    Name = RichText.Clean(info.Name),
                    Role = RoleName(info),
                    Level = info.XPLevel,
                    Alive = !IsDead(info),
                    Condition = ThoughtName(info.CursedState, info.ID),
                    Species = Species(info),
                    Headline = Headline(info, brain),
                    TraitCount = info.Traits?.Count ?? 0,
                    Disciple = info.IsDisciple,
                    MarriedToLeader = info.MarriedToLeader,
                    Spouse = SpouseName(info),
                    Age = info.Age,
                    MemberDays = info.MemberDuration,
                };

                // Mirrors FollowerInformationBox.ConfigureImpl: loyalty is hidden outright for
                // a mutated follower, and food and illness are hidden together whenever there
                // is no live brain behind the info — a dead or not-yet-spawned follower.
                snapshot.LoyaltyShown = !HasTrait(info, FollowerTrait.TraitType.Mutated);
                snapshot.Loyalty = brain != null
                    ? Percent(brain.Stats.Adoration / brain.Stats.MAX_ADORATION)
                    : Percent(info.Adoration / 100f);

                snapshot.NeedsShown = brain != null;
                if (brain != null)
                {
                    snapshot.Food = Percent(
                        (brain.Stats.Satiation + (StarvationRange - brain.Stats.Starvation)) /
                        FoodRange);
                    snapshot.Health = Percent(1f - info.Illness / 100f);
                    snapshot.BiggestNeed = BiggestNeed(brain, info);
                    if (includeTask) snapshot.Task = TaskName(brain);
                }

                // The pleasure bar only exists once the Pleasure Shrine is built; without a
                // card to ask, that flag is the same gate the game itself uses.
                snapshot.PleasureShown = PleasureUnlocked();
                snapshot.Pleasure = Percent(info.Pleasure / PleasureRange);

                return snapshot;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[follower] could not read follower state: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Replace the derived values with the card's own live fills and container flags.
        /// This is what makes the reading match the screen exactly rather than approximately.
        /// </summary>
        private static void ReadCardFills(FollowerInformationBox box, FollowerSnapshot snapshot)
        {
            try
            {
                var adorationContainer = Field(AdorationContainerRef, box);
                if (adorationContainer != null)
                    snapshot.LoyaltyShown = adorationContainer.activeSelf;
                var adoration = Field(AdorationLevelRef, box);
                if (adoration != null) snapshot.Loyalty = Percent(adoration.fillAmount);

                // The game toggles the grandparent of each bar image, which is the row that
                // holds the icon and the bar together.
                var hungerShown = RowShown(box.HungerLevel);
                var illnessShown = RowShown(box.IllnessLevel);
                snapshot.NeedsShown = hungerShown && illnessShown;
                if (box.HungerLevel != null) snapshot.Food = Percent(box.HungerLevel.fillAmount);
                if (box.IllnessLevel != null) snapshot.Health = Percent(box.IllnessLevel.fillAmount);

                var pleasureContainer = Field(PleasureContainerRef, box);
                if (pleasureContainer != null)
                    snapshot.PleasureShown = pleasureContainer.activeSelf;
                var pleasure = Field(PleasureLevelRef, box);
                if (pleasure != null) snapshot.Pleasure = Percent(pleasure.fillAmount);
            }
            catch (Exception e)
            {
                // The derived values are already in place, so a changed field name costs
                // accuracy rather than the whole reading.
                Plugin.Log.LogWarning(
                    $"[follower] could not read the card's own bars, using derived values: {e.Message}");
            }
        }

        private static bool RowShown(Image bar)
        {
            var row = bar?.transform?.parent?.parent;
            return row != null && row.gameObject.activeSelf;
        }

        /// <summary>
        /// The summary screen's own "what is wrong with this follower" chain, in its order.
        /// Reproduced rather than invented, because the game already answers this question in
        /// one line and that line is the one a sighted player reads.
        ///
        /// The first three branches restate a cursed state the reading already names, so they
        /// are dropped: "ill, needs medicine" says the same thing twice.
        /// </summary>
        private static string BiggestNeed(FollowerBrain brain, FollowerInfo info)
        {
            var state = info.CursedState;
            if (state == Thought.OldAge || state == Thought.Dissenter ||
                state == Thought.Ill || state == Thought.BecomeStarving)
                return string.Empty;

            if (brain.Stats.Exhaustion > 0f)
                return ThoughtName(Thought.BiggestNeed_Exhausted, info.ID);
            if (!brain.HasHome)
                return ThoughtName(Thought.BiggestNeed_Homeless, info.ID);

            var bed = StructureManager.GetStructureByID<Structures_Bed>(
                brain._directInfoAccess.DwellingID);
            return bed != null && bed.IsCollapsed
                ? ThoughtName(Thought.BiggestNeed_BrokenBed, info.ID)
                : string.Empty;
        }

        /// <summary>
        /// What the follower is doing. There is no localisation key for any of the 137 task
        /// types — the game conveys the task through animation and position only — so the enum
        /// name is humanised. That is the same last resort the form picker uses for an
        /// unnamed skin, and it is honest: a slightly stiff name still says what they are
        /// doing, and silence does not.
        /// </summary>
        private static string TaskName(FollowerBrain brain)
        {
            var type = brain.CurrentTaskType;
            return type == FollowerTaskType.None
                ? string.Empty
                : RichText.Humanise(type.ToString()).ToLowerInvariant();
        }

        private static string RoleName(FollowerInfo info)
        {
            var localized = FollowerRoleInfo.GetLocalizedName(info.FollowerRole);
            return RichText.IsUsableLocalization(localized, $"Traits/{info.FollowerRole}")
                ? RichText.Clean(localized)
                : RichText.Humanise(info.FollowerRole.ToString());
        }


        /// <summary>
        /// The game's own authored title for the follower's form — goat, deer, snake.
        ///
        /// This is what a sighted player identifies a follower by from across the base, before
        /// any name is legible, and the overhead name is off unless the player turns on
        /// `ShowFollowerNames` in the game's Gameplay settings. `WorshipperData` resolves the
        /// saved skin identifier to a title the wiki and other players use; the placeholder
        /// "Character Name" and an empty title both mean the form has no authored name, so
        /// nothing is claimed rather than an internal identifier being read out.
        /// </summary>
        internal static string Species(FollowerInfo info)
        {
            try
            {
                var skin = info?.SkinName;
                if (string.IsNullOrEmpty(skin)) return string.Empty;

                var data = WorshipperData.Instance?.GetCharacters(skin);
                var title = data?.Title;
                if (string.IsNullOrEmpty(title) || title == "Character Name") return string.Empty;

                // Measured 2026-08-25: these authored titles are a mixture. "Chicken" and
                // "Poppy" are real names; "DeerRitual" is a run-together identifier that
                // reached the player as one word. Humanise splits it into "Deer Ritual",
                // which is the game's own name made pronounceable rather than a name of ours.
                var clean = RichText.Clean(title);
                return NeedsSplitting(clean) ? RichText.Humanise(clean) : clean;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[follower] could not read a follower's form: {e.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// True for a single word carrying an internal capital — "DeerRitual" — which is an
        /// identifier that escaped into an authored field rather than a name anyone wrote to
        /// be read. A title that already contains a space is left exactly as authored.
        /// </summary>
        private static bool NeedsSplitting(string title)
        {
            if (title.Length < 2 || title.IndexOf(' ') >= 0) return false;

            for (var i = 1; i < title.Length; i++)
                if (char.IsUpper(title[i]) && !char.IsUpper(title[i - 1])) return true;

            return false;
        }

        /// <summary>
        /// The one thing worth walking over to this follower for.
        ///
        /// `interaction_FollowerInteraction.GetLabel` already ranks exactly this, and its order
        /// is reproduced here rather than invented: protect a follower lightning is about to
        /// strike, catch a spy leaving the cult, absolve sin, complete a quest, collect a
        /// levelling reward. The label itself cannot be used — it is empty until the player is
        /// adjacent and empty again outside the base — so each clause is derived from the same
        /// field the label branches on, which is the fix shape this project keeps arriving at.
        ///
        /// One entry is not in the label: a follower whose current task is
        /// `GetPlayerAttention` is walking towards the player specifically to ask for
        /// something. It sits last so the game's own ranking wins wherever the two overlap.
        /// </summary>
        internal static string Headline(FollowerInfo info, FollowerBrain brain)
        {
            if (info == null) return string.Empty;

            try
            {
                if (brain != null)
                {
                    if (brain.CurrentTaskType == FollowerTaskType.LeaveCult &&
                        info.CursedState != Thought.Dissenter &&
                        brain.HasTrait(FollowerTrait.TraitType.Spy))
                        return GameTerm("Interactions/Catch", "follower.headline_catch");

                    if (brain.CurrentTaskType == FollowerTaskType.Floating || brain.CanGiveSin())
                        return Localization.Strings.Get("follower.headline_sin");
                }

                var completed = DataManager.Instance?.CompletedQuestFollowerIDs;
                if (completed != null && completed.Contains(info.ID))
                    return GameTerm("Interactions/CompleteQuest", "follower.headline_quest");

                if (brain != null && brain.CanLevelUp())
                    return GameTerm(
                        "Interactions/CollectDiscipleReward", "follower.headline_reward");

                if (brain != null && brain.CurrentTaskType == FollowerTaskType.GetPlayerAttention)
                {
                    var reason = AttentionReason(brain.CurrentTask as FollowerTask_GetAttention);
                    return reason.Length == 0
                        ? Localization.Strings.Get("follower.headline_attention")
                        : reason;
                }

                return string.Empty;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[follower] could not rank a follower: {e.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Why a follower is coming to find the player. The game carries this as a typed
        /// `ComplaintType` on the task and shows it as a generic help bubble, so the type is
        /// real data and the words are ours — there is no localised string for any of them.
        /// </summary>
        internal static string AttentionReason(FollowerTask_GetAttention task)
        {
            if (task == null) return string.Empty;

            switch (task.ComplaintType)
            {
                case Follower.ComplaintType.Hunger:
                    return Localization.Strings.Get("follower.complaint_hunger");
                case Follower.ComplaintType.Homeless:
                    return Localization.Strings.Get("follower.complaint_homeless");
                case Follower.ComplaintType.Sick:
                    return Localization.Strings.Get("follower.complaint_sick");
                case Follower.ComplaintType.ReadyToLevelUp:
                    return Localization.Strings.Get("follower.complaint_level_up");
                case Follower.ComplaintType.NeedBetterHouse:
                    return Localization.Strings.Get("follower.complaint_better_house");
                case Follower.ComplaintType.FirstTimeSpeakingToPlayer:
                    return Localization.Strings.Get("follower.complaint_first_meeting");
                case Follower.ComplaintType.Grateful:
                    return Localization.Strings.Get("follower.complaint_grateful");
                case Follower.ComplaintType.GiveQuest:
                    return Localization.Strings.Get("follower.complaint_give_quest");
                case Follower.ComplaintType.CompletedQuest:
                    return Localization.Strings.Get("follower.complaint_completed_quest");
                case Follower.ComplaintType.FailedQuest:
                    return Localization.Strings.Get("follower.complaint_failed_quest");
                case Follower.ComplaintType.GiveOnboarding:
                    return Localization.Strings.Get("follower.complaint_onboarding");
                case Follower.ComplaintType.ShowTwitchMessage:
                    return Localization.Strings.Get("follower.complaint_twitch");
                case Follower.ComplaintType.Speak:
                    return Localization.Strings.Get("follower.complaint_speak");
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// The game's own word for something, falling back to the mod's catalogue when the
        /// term is untranslated. Preferred wherever the game already names the thing, so the
        /// player hears what the wiki and their friends use.
        /// </summary>
        private static string GameTerm(string term, string fallbackKey)
        {
            var localized = LocalizationManager.GetTranslation(term);
            return RichText.IsUsableLocalization(localized, term)
                ? RichText.Clean(localized).ToLowerInvariant()
                : Localization.Strings.Get(fallbackKey);
        }

        /// <summary>The localised name of a follower's cursed state, or empty.</summary>
        internal static string ConditionName(FollowerInfo info) =>
            info == null ? string.Empty : ThoughtName(info.CursedState, info.ID);

        private static string ThoughtName(Thought thought, int followerId)
        {
            if (thought == Thought.None) return string.Empty;

            var localized = FollowerThoughts.GetLocalisedName(thought, followerId);
            return RichText.IsUsableLocalization(localized)
                ? RichText.Clean(localized)
                : RichText.Humanise(thought.ToString());
        }

        /// <summary>
        /// The localised reason the current screen will not accept this follower. The game
        /// has 38 of these and already resolves each one to a sentence, so none of it is
        /// invented here.
        /// </summary>
        private static string UnavailableReason(FollowerSelectEntry entry)
        {
            if (entry == null || entry.AvailabilityStatus == FollowerSelectEntry.Status.Available)
                return string.Empty;

            var plain = Localization.Strings.Get("follower.unavailable_plain");
            if (entry.AvailabilityStatus == FollowerSelectEntry.Status.Unavailable) return plain;

            var term = $"UI/FollowerSelect/{entry.AvailabilityStatus}";
            var localized = LocalizationManager.GetTranslation(term);
            return RichText.IsUsableLocalization(localized, term)
                ? Localization.Strings.Format("follower.unavailable", RichText.Clean(localized))
                : plain;
        }

        private static string SpouseName(FollowerInfo info)
        {
            if (info.SpouseFollowerID == -1) return string.Empty;

            var spouse = FollowerInfo.GetInfoByID(info.SpouseFollowerID, includeDead: true);
            return spouse == null ? string.Empty : RichText.Clean(spouse.Name);
        }

        /// <summary>
        /// The brain matching this info, or null. Found by walking the game's own live list,
        /// exactly as <c>FollowerInformationBox</c> does — deliberately not
        /// <c>FollowerBrain.GetOrCreateBrain</c>, which would manufacture a brain for a
        /// follower that does not have one as a side effect of being asked about.
        /// </summary>
        private static FollowerBrain LiveBrain(int id)
        {
            var brains = FollowerBrain.AllBrains;
            if (brains == null) return null;

            foreach (var brain in brains)
                if (brain != null && brain.Info != null && brain.Info.ID == id)
                    return brain;

            return null;
        }

        private static bool IsDead(FollowerInfo info)
        {
            var dead = DataManager.Instance?.Followers_Dead;
            return dead != null && dead.Contains(info);
        }

        private static bool HasTrait(FollowerInfo info, FollowerTrait.TraitType trait) =>
            info.Traits != null && info.Traits.Contains(trait);

        private static bool PleasureUnlocked()
        {
            try { return DataManager.Instance != null && DataManager.Instance.PleasureEnabled; }
            catch (Exception) { return false; }
        }

        internal static int Percent(float normalised)
        {
            var value = (int)Math.Round(normalised * 100f);
            if (value < 0) return 0;
            return value > 100 ? 100 : value;
        }

        /// <summary>
        /// A field reference that reports rather than throws at class-load time. A renamed
        /// private field after a game update should cost this one value, not every static
        /// initializer in the type.
        /// </summary>
        private static AccessTools.FieldRef<TOwner, TField> SafeField<TOwner, TField>(string name)
        {
            try { return AccessTools.FieldRefAccess<TOwner, TField>(name); }
            catch (Exception e)
            {
                Plugin.Log.LogWarning(
                    $"[follower] {typeof(TOwner).Name}.{name} not found: {e.Message}");
                return null;
            }
        }

        private static TField Field<TOwner, TField>(
            AccessTools.FieldRef<TOwner, TField> reference, TOwner owner)
            where TOwner : class =>
            reference == null || owner == null ? default : reference(owner);
    }
}
