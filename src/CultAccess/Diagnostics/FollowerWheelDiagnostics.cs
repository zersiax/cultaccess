using System.Collections.Generic;
using System.Text;
using CultAccess.Util;
using Lamb.UI.FollowerInteractionWheel;

namespace CultAccess.Diagnostics
{
    /// <summary>
    /// Log-only evidence for a question that has been open since before the follower pass:
    /// when a command is not on a follower's wheel, is it not unlocked, or is it simply in a
    /// different state's group?
    ///
    /// <c>FollowerCommandGroups.DefaultCommands</c> is a priority-ordered dispatcher over the
    /// follower's own state — snowman, hibernating, child, zombie, asleep, drunk, dissenting,
    /// and on down — each branch returning a wholly different list. So a missing command has
    /// two entirely different causes that look identical from the outside, and guessing between
    /// them is exactly the confident-wrong-diagnosis this project keeps paying for.
    ///
    /// Rather than re-implementing that dispatcher, which would duplicate it and then drift
    /// from it, this records the inputs it branches on, the list it actually produced, and the
    /// unlock flags for the four doctrine-gated commands. Those three together settle the
    /// question from one session without another play cycle.
    ///
    /// Never speaks. The wheel is a modal the player is mid-decision in, and a probe that
    /// talked there would be the worst possible place for it.
    /// </summary>
    internal static class FollowerWheelDiagnostics
    {
        internal static void LogWheelOpened(Follower follower, List<CommandItem> commands)
        {
            if (!Plugin.LogFollowerWheel.Value) return;

            try
            {
                Plugin.Log.LogInfo(
                    $"[follower wheel state] {FollowerState(follower)} {Unlocks()}");
                Plugin.Log.LogInfo(
                    $"[follower wheel state] commands={Commands(follower, commands)}");
            }
            catch (System.Exception e)
            {
                // A probe must never be able to take down the screen it is watching.
                Plugin.Log.LogWarning($"[follower wheel state] could not be recorded: {e.Message}");
            }
        }

        /// <summary>
        /// Every field `DefaultCommands` branches on, in roughly the order it tests them, so a
        /// log line can be read straight down against the source.
        /// </summary>
        private static string FollowerState(Follower follower)
        {
            var info = follower?.Brain?._directInfoAccess;
            if (info == null) return "follower=unreadable";

            var brain = follower.Brain;
            var task = brain.CurrentTask;

            return
                $"follower=\"{RichText.Clean(info.Name)}\" id={info.ID} " +
                $"cursed={info.CursedState} task={brain.CurrentTaskType} " +
                $"taskState={(task == null ? "none" : task.State.ToString())} " +
                $"role={info.FollowerRole} level={info.XPLevel} " +
                $"snowman={info.IsSnowman} drunk={info.IsDrunk} " +
                $"disciple={info.IsDisciple} orders={brain.Stats.WorkerBeenGivenOrders} " +
                $"traits={Traits(brain)}";
        }

        /// <summary>
        /// The traits that select a group of their own. Named individually rather than dumping
        /// the whole list, so the line stays greppable.
        /// </summary>
        private static string Traits(FollowerBrain brain)
        {
            var names = new List<string>(6);
            Add(names, brain, FollowerTrait.TraitType.Zombie);
            Add(names, brain, FollowerTrait.TraitType.Mutated);
            Add(names, brain, FollowerTrait.TraitType.Spy);
            Add(names, brain, FollowerTrait.TraitType.Scared);
            Add(names, brain, FollowerTrait.TraitType.Hibernation);
            Add(names, brain, FollowerTrait.TraitType.Aestivation);
            Add(names, brain, FollowerTrait.TraitType.ExistentialDread);
            return names.Count == 0 ? "none" : string.Join("+", names.ToArray());
        }

        private static void Add(
            List<string> names, FollowerBrain brain, FollowerTrait.TraitType trait)
        {
            if (brain.HasTrait(trait)) names.Add(trait.ToString());
        }

        /// <summary>
        /// The gates that are *not* about which group was chosen. A command absent here is
        /// absent for everyone, which is precisely the distinction the log has to draw.
        /// </summary>
        private static string Unlocks()
        {
            var data = DataManager.Instance;
            return
                $"murderUnlocked={Doctrine(DoctrineUpgradeSystem.DoctrineType.LawOrder_MurderFollower)} " +
                $"extortUnlocked={Doctrine(DoctrineUpgradeSystem.DoctrineType.Possessions_ExtortTithes)} " +
                $"bribeUnlocked={Doctrine(DoctrineUpgradeSystem.DoctrineType.Possessions_Bribe)} " +
                $"inspireUnlocked={Doctrine(DoctrineUpgradeSystem.DoctrineType.WorkWorship_Inspire)} " +
                $"intimidateUnlocked={Doctrine(DoctrineUpgradeSystem.DoctrineType.WorkWorship_Intimidate)} " +
                $"surveillance={(data != null && data.HasBuiltSurveillance)} " +
                $"canReadMinds={(data != null && data.CanReadMinds)} " +
                $"loyaltyBars={(data != null && data.ShowLoyaltyBars)}";
        }

        private static bool Doctrine(DoctrineUpgradeSystem.DoctrineType type)
        {
            try { return DoctrineUpgradeSystem.GetUnlocked(type); }
            catch (System.Exception) { return false; }
        }

        /// <summary>
        /// What the wheel actually offered, with each entry's own availability and whether it
        /// opens a submenu rather than acting. `MurderCommandItem`, for instance, is always
        /// available once unlocked and always opens a confirmation, so "present but greyed"
        /// and "present and immediate" are different answers worth telling apart.
        /// </summary>
        private static string Commands(Follower follower, List<CommandItem> commands)
        {
            if (commands == null || commands.Count == 0) return "none";

            var builder = new StringBuilder();
            foreach (var command in commands)
            {
                if (command == null) continue;
                if (builder.Length > 0) builder.Append(' ');

                var available = false;
                try { available = command.IsAvailable(follower); }
                catch (System.Exception) { }

                var submenu = command.SubCommands != null && command.SubCommands.Count > 0;
                builder.Append($"{command.Command}:{(available ? "yes" : "no")}");
                if (submenu) builder.Append("+sub");
            }

            return builder.Length == 0 ? "none" : builder.ToString();
        }
    }
}
