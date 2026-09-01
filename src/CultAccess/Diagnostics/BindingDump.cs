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

        /// <summary>
        /// Dump what the connected controller physically exposes, what the game binds on it,
        /// and therefore what is left.
        ///
        /// The outstanding half of the input gate. Until now only the keyboard was probed, so
        /// every controller statement in the docs was reasoning rather than evidence — and the
        /// keyboard-only dump is exactly why a session played entirely on a pad produced no
        /// usable input data at all.
        ///
        /// Two things make this harder than the keyboard case and are worth stating rather
        /// than rediscovering. Rewired only instantiates joystick maps for a controller that
        /// is actually connected, so this is silent and says so if the pad is absent, rather
        /// than reporting an empty set as "nothing is bound". And bindings are per map
        /// category: an element free during gameplay may well be taken in menus, so the free
        /// set is reported per category as well as overall. Only the overall set is safe for
        /// a global hotkey; a per-category gap is usable only by something that knows which
        /// context it is in.
        /// </summary>
        public static void LogControllerBindings()
        {
            try
            {
                if (!ReInput.isReady)
                {
                    Plugin.Log.LogInfo("[controller probe] Rewired not ready; skipped.");
                    return;
                }

                var joysticks = ReInput.controllers.Joysticks;
                if (joysticks == null || joysticks.Count == 0)
                {
                    Plugin.Log.LogInfo(
                        "[controller probe] no joystick connected; nothing to probe. Rewired " +
                        "only builds joystick maps for a controller that is present, so this " +
                        "is an absent pad rather than an unbound one. Connect it and restart.");
                    return;
                }

                foreach (var joystick in joysticks)
                {
                    if (joystick == null) continue;
                    LogOneJoystick(joystick);
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[controller probe] failed: {e}");
            }
        }

        private static void LogOneJoystick(Joystick joystick)
        {
            var elements = joystick.ElementIdentifiers;
            var report = new StringBuilder();
            report.AppendLine(
                $"[controller probe] joystick=\"{joystick.name}\" " +
                $"hardware=\"{joystick.hardwareName}\" elements={elements?.Count ?? 0}");

            if (elements == null || elements.Count == 0)
            {
                Plugin.Log.LogInfo(report.ToString());
                return;
            }

            // Element id to a readable "name (Type)", so the free list below names things the
            // way a player would say them rather than by Rewired's numbering.
            var known = new SortedDictionary<int, string>();
            foreach (var element in elements)
                known[element.id] = $"{element.name} ({element.elementType})";

            // categoryName -> set of element ids bound in it.
            var boundByCategory = new SortedDictionary<string, SortedSet<int>>();
            var boundAnywhere = new SortedSet<int>();

            foreach (var player in ReInput.players.AllPlayers)
            {
                foreach (var map in player.controllers.maps.GetAllMaps(ControllerType.Joystick))
                {
                    if (map == null) continue;

                    var category = ReInput.mapping.GetMapCategory(map.categoryId);
                    var categoryName =
                        category?.descriptiveName ?? category?.name ?? $"category {map.categoryId}";

                    if (!boundByCategory.TryGetValue(categoryName, out var inCategory))
                    {
                        inCategory = new SortedSet<int>();
                        boundByCategory[categoryName] = inCategory;
                    }

                    foreach (var binding in map.AllMaps)
                    {
                        if (binding == null) continue;

                        inCategory.Add(binding.elementIdentifierId);
                        boundAnywhere.Add(binding.elementIdentifierId);

                        var action = ReInput.mapping.GetAction(binding.actionId);
                        var actionName =
                            action?.descriptiveName ?? action?.name ?? $"action {binding.actionId}";

                        report.AppendLine(
                            $"  bound category=\"{categoryName}\" " +
                            $"element=\"{binding.elementIdentifierName}\" " +
                            $"id={binding.elementIdentifierId} type={binding.elementType} " +
                            $"pole={binding.axisContribution} action=\"{actionName}\" " +
                            $"actionId={binding.actionId}");
                    }
                }
            }

            foreach (var pair in boundByCategory)
                report.AppendLine(
                    $"  FREE in \"{pair.Key}\" ({known.Count - pair.Value.Count}): " +
                    Join(known, pair.Value));

            // The only line that answers "what can a global mod hotkey use?". Anything here is
            // untouched by every category the game has loaded.
            report.AppendLine(
                $"  FREE EVERYWHERE ({known.Count - boundAnywhere.Count}): " +
                Join(known, boundAnywhere));

            Plugin.Log.LogInfo(report.ToString());
        }

        private static string Join(
            SortedDictionary<int, string> known, SortedSet<int> excluded)
        {
            var free = new List<string>();
            foreach (var pair in known)
                if (!excluded.Contains(pair.Key)) free.Add(pair.Value);

            return free.Count == 0 ? "none" : string.Join(", ", free.ToArray());
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
