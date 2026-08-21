using System.Collections.Generic;
using System.Text;
using Rewired;

namespace CultAccess.Diagnostics
{
    /// <summary>
    /// Logs every keyboard key the game itself binds.
    ///
    /// The mod's own hotkeys must not collide with gameplay input, and the bindings live in
    /// Rewired's data rather than in code, so they cannot be read from the decompiled
    /// source — only from the running game. This dumps them once at startup so the safe-key
    /// set can be chosen from evidence instead of assumption.
    ///
    /// Log only, never spoken: diagnostics that talk would flood the screen reader during
    /// exactly the sessions where the log matters.
    /// </summary>
    internal static class BindingDump
    {
        /// <summary>
        /// Return the live keyboard bindings for one Rewired action.  This is also used
        /// by spoken control hints, so a remapped key is described instead of a baked-in
        /// default.  Axis actions can be restricted to their negative or positive pole
        /// (for example A versus D on UI Horizontal).
        /// </summary>
        internal static string KeyboardBindingsForAction(int actionId, Pole? pole = null)
        {
            try
            {
                if (!ReInput.isReady) return string.Empty;

                var names = new SortedSet<string>();
                foreach (var player in ReInput.players.AllPlayers)
                {
                    foreach (var map in player.controllers.maps.GetAllMaps(ControllerType.Keyboard))
                    {
                        if (map == null) continue;

                        foreach (var binding in map.AllMaps)
                        {
                            if (binding == null || binding.actionId != actionId) continue;
                            if (pole.HasValue && binding.axisContribution != pole.Value) continue;

                            var name = binding.elementIdentifierName;
                            if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
                        }
                    }
                }

                return string.Join(" or ", new List<string>(names).ToArray());
            }
            catch
            {
                // Hints are optional. A transient Rewired rebuild while controls are
                // being remapped must never interfere with menu navigation.
                return string.Empty;
            }
        }

        public static void LogKeyboardBindings()
        {
            try
            {
                if (!ReInput.isReady)
                {
                    Plugin.Log.LogInfo("Rewired not ready; skipping binding dump.");
                    return;
                }

                var used = new SortedDictionary<string, List<string>>();

                foreach (var player in ReInput.players.AllPlayers)
                {
                    foreach (var map in player.controllers.maps.GetAllMaps(ControllerType.Keyboard))
                    {
                        if (map == null) continue;

                        foreach (var binding in map.AllMaps)
                        {
                            if (binding == null) continue;

                            var key = binding.elementIdentifierName;
                            if (string.IsNullOrEmpty(key)) continue;

                            var action = ReInput.mapping.GetAction(binding.actionId);
                            var actionName = action?.descriptiveName ?? action?.name ?? $"action {binding.actionId}";

                            if (!used.TryGetValue(key, out var actions))
                            {
                                actions = new List<string>();
                                used[key] = actions;
                            }

                            if (!actions.Contains(actionName)) actions.Add(actionName);
                        }
                    }
                }

                if (used.Count == 0)
                {
                    Plugin.Log.LogInfo("Rewired reported no keyboard bindings.");
                    return;
                }

                var report = new StringBuilder();
                report.AppendLine($"Game keyboard bindings in use ({used.Count} keys) — avoid these for mod hotkeys:");
                foreach (var pair in used)
                    report.AppendLine($"  {pair.Key} -> {string.Join(", ", pair.Value.ToArray())}");

                Plugin.Log.LogInfo(report.ToString());
            }
            catch (System.Exception e)
            {
                // A failed dump must never affect startup; it is purely informational.
                Plugin.Log.LogWarning($"Binding dump failed: {e.Message}");
            }
        }
    }
}
