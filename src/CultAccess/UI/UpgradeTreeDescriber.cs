using System;
using System.Collections.Generic;
using System.Reflection;
using CultAccess.Diagnostics;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;
using Rewired;
using UnityEngine;
using UnityEngine.UI;

namespace CultAccess.UI
{
    /// <summary>Reads upgrade-tree nodes and the otherwise focusless unlock overlay.</summary>
    internal static class UpgradeTreeDescriber
    {
        private static readonly FieldInfo OverlayNodeField = AccessTools.Field(
            typeof(UIUpgradeUnlockOverlayControllerBase), "_node");

        private static UpgradeSystem.Type _pendingUpgrade = UpgradeSystem.Type.Count;
        private static float _pendingUntil;

        internal static bool TryDescribe(Selectable selectable, out string description)
        {
            description = string.Empty;
            if (selectable == null) return false;

            var node = selectable.GetComponentInParent<UpgradeTreeNode>();
            if (node == null) return false;
            description = DescribeNode(node, includeControl: true);
            return true;
        }

        internal static bool TryDescribeContext(Selectable selectable, out string context)
        {
            context = string.Empty;
            var node = selectable?.GetComponentInParent<UpgradeTreeNode>();
            if (node == null) return false;

            if (node.GetComponentInParent<UIUpgradePlayerTreeMenuController>() != null)
            {
                context = $"Crown ability tree. Direction keys move between connected " +
                          $"upgrades. {Key(38, "confirm")} opens details. " +
                          $"{Key(39, "back")} goes back";
                return true;
            }

            if (node.GetComponentInParent<UIUpgradeTreeMenuController>() != null)
            {
                context = $"Divine Inspiration upgrade tree. " +
                          $"{UpgradeSystem.AbilityPoints} Divine Inspiration available. " +
                          $"Direction keys move between connected upgrades. " +
                          $"{Key(38, "confirm")} opens details. " +
                          $"{Key(43, "previous tree")} and {Key(44, "next tree")} " +
                          $"switch trees when available. {Key(39, "back")} goes back";
                return true;
            }

            return false;
        }

        internal static string DescribeNode(UpgradeTreeNode node, bool includeControl)
        {
            if (node == null) return "Upgrade details unavailable";

            var parts = new List<string>
            {
                UpgradeName(node.Upgrade),
                $"tier {(int)node.NodeTier + 1}",
                StateName(node.State),
            };

            var description = RichText.Clean(
                UpgradeSystem.GetLocalizedDescription(node.Upgrade));
            if (description.Length > 0) parts.Add(description);

            var requirement = Requirement(node);
            if (requirement.Length > 0) parts.Add(requirement);

            var menu = node.GetComponentInParent<UIMenuBase>();
            var nodes = menu?.GetComponentsInChildren<UpgradeTreeNode>(false);
            var index = nodes == null ? -1 : Array.IndexOf(nodes, node);
            if (index >= 0)
                parts.Add(ProgressionText.Position("visible node", index, nodes.Length));

            if (includeControl)
                parts.Add($"{Key(38, "confirm")} opens details");

            return string.Join(". ", parts.ToArray());
        }

        internal static void AnnounceOverlay(UIUpgradeUnlockOverlayControllerBase overlay)
        {
            if (overlay == null || !Plugin.SpeechEnabled.Value) return;

            UpgradeTreeNode node;
            try { node = OverlayNodeField?.GetValue(overlay) as UpgradeTreeNode; }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read upgrade confirmation node: {e.Message}");
                return;
            }
            if (node == null) return;

            _pendingUpgrade = node.Upgrade;
            _pendingUntil = Time.unscaledTime + 15f;

            var details = DescribeNode(node, includeControl: false);
            var available = node.State == UpgradeTreeNode.NodeState.Available &&
                            (!(overlay is UIUpgradeUnlockOverlayController) ||
                             UpgradeSystem.AbilityPoints > 0);
            string instruction;
            if (!available)
            {
                instruction = $"Cannot unlock. {Key(39, "back")} closes";
            }
            else if (SettingsManager.Settings.Accessibility.HoldActions)
            {
                instruction = $"{ProgressionText.Confirm(Key(38, "confirm"), true, "unlock")}. " +
                              $"{Key(39, "back")} cancels";
            }
            else
            {
                instruction = $"{ProgressionText.Confirm(Key(38, "confirm"), false, "unlock")}. " +
                              $"{Key(39, "back")} cancels";
            }

            Plugin.Log.LogInfo(
                $"[upgrade tree] event=confirmation upgrade={node.Upgrade} " +
                $"state={node.State} available={available}");
            Speaker.SayParts(SpeechPriority.Now, details, instruction);
        }

        internal static void AnnounceUnlocked(UpgradeSystem.Type type)
        {
            if (!Plugin.SpeechEnabled.Value || !ShouldAnnounceUnlock(type)) return;

            _pendingUpgrade = UpgradeSystem.Type.Count;
            _pendingUntil = 0f;
            var name = UpgradeName(type);
            var effect = RichText.Clean(UpgradeSystem.GetLocalizedDescription(type));
            Plugin.Log.LogInfo($"[upgrade tree] event=unlocked upgrade={type}");
            Speaker.SayParts(
                SpeechPriority.Now,
                $"Upgrade unlocked. {name}",
                effect);
        }

        private static bool ShouldAnnounceUnlock(UpgradeSystem.Type type)
        {
            if (_pendingUpgrade == type && Time.unscaledTime <= _pendingUntil) return true;

            var menus = UIMenuBase.ActiveMenus;
            if (menus == null) return false;
            foreach (var menu in menus)
            {
                if (menu is UIUpgradeTreeMenuController ||
                    menu is UIUpgradePlayerTreeMenuController ||
                    menu is UIUpgradeUnlockOverlayControllerBase)
                    return true;
            }
            return false;
        }

        private static string Requirement(UpgradeTreeNode node)
        {
            switch (node.State)
            {
                case UpgradeTreeNode.NodeState.Hidden:
                    return "hidden until its prerequisite upgrade is unlocked";

                case UpgradeTreeNode.NodeState.Locked:
                {
                    var tier = node.TierConfig;
                    if (tier == null ||
                        DataManager.Instance.CurrentUpgradeTreeTier <= node.NodeTier ||
                        !tier.RequiresCentralTier ||
                        tier.CentralNode != node.Upgrade ||
                        node.TreeConfig.NumUnlockedUpgrades() <
                        node.TreeConfig.NumRequiredNodesForTier(node.NodeTier))
                        return $"requires upgrade tree tier {(int)node.NodeTier + 1}";

                    if (node.RequiresBuiltStructure != StructureBrain.TYPES.NONE)
                    {
                        var structure = RichText.Clean(
                            StructuresData.LocalizedName(node.RequiresBuiltStructure));
                        if (structure.Length == 0)
                            structure = RichText.Humanise(node.RequiresBuiltStructure.ToString());
                        return $"requires built structure: {structure}";
                    }
                    return "locked";
                }

                case UpgradeTreeNode.NodeState.Unavailable:
                {
                    var prerequisites = new List<string>();
                    foreach (var prerequisite in node.PrerequisiteNodes)
                    {
                        if (prerequisite != null)
                            prerequisites.Add(UpgradeName(prerequisite.Upgrade));
                    }
                    return prerequisites.Count == 0
                        ? "prerequisite unavailable"
                        : $"requires one of: {string.Join(", ", prerequisites.ToArray())}";
                }

                case UpgradeTreeNode.NodeState.Available:
                    if (node.GetComponentInParent<UIUpgradeTreeMenuController>() != null &&
                        UpgradeSystem.AbilityPoints <= 0)
                        return "requires 1 Divine Inspiration, 0 available";
                    return string.Empty;

                default:
                    return string.Empty;
            }
        }

        private static string StateName(UpgradeTreeNode.NodeState state)
        {
            switch (state)
            {
                case UpgradeTreeNode.NodeState.Hidden: return "hidden";
                case UpgradeTreeNode.NodeState.Locked: return "locked";
                case UpgradeTreeNode.NodeState.Unavailable: return "prerequisite not met";
                case UpgradeTreeNode.NodeState.Available: return "available";
                case UpgradeTreeNode.NodeState.Unlocked: return "unlocked";
                default: return RichText.Humanise(state.ToString());
            }
        }

        private static string UpgradeName(UpgradeSystem.Type type)
        {
            var name = RichText.Clean(UpgradeSystem.GetLocalizedName(type));
            return name.Length > 0 ? name : RichText.Humanise(type.ToString());
        }

        private static string Key(int action, string fallback)
        {
            var key = BindingDump.KeyboardBindingsForAction(action);
            return key.Length > 0 ? key : fallback;
        }
    }

    [HarmonyPatch]
    internal static class UpgradeTreePatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIUpgradeUnlockOverlayControllerBase), "OnShowCompleted")]
        private static void AfterUnlockOverlayShown(
            UIUpgradeUnlockOverlayControllerBase __instance) =>
            UpgradeTreeDescriber.AnnounceOverlay(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UpgradeSystem), nameof(UpgradeSystem.UnlockAbility),
            new[] { typeof(UpgradeSystem.Type), typeof(bool) })]
        private static void AfterUpgradeUnlocked(UpgradeSystem.Type __0, bool __result)
        {
            if (__result) UpgradeTreeDescriber.AnnounceUnlocked(__0);
        }
    }
}
