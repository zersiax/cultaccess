using System.Collections.Generic;
using CultAccess.Speech;
using Rewired;
using UnityEngine;

namespace CultAccess.Input
{
    /// <summary>
    /// Mod hotkeys on the controller, as a layer held open with the left trigger.
    ///
    /// Not a design choice so much as the only thing the hardware allows. The 2026-08-23
    /// binding probe found exactly one genuinely free element on an XInput pad: the left
    /// trigger, unbound in both gameplay and menus and used only by photo mode. The three
    /// elements reported free in every category were two Rewired compound-stick aliases,
    /// which are not separately pressable, and the Guide button, which Steam and Windows both
    /// intercept. Everything else on the pad carries three or four actions already.
    ///
    /// Why it matters: an entire session went by without the enemy beacon being engaged once,
    /// and the reason was not that anyone disliked it. Play is on a controller, and putting
    /// the pad down to reach a keyboard costs more than the beacon is worth mid-fight. A
    /// combat feature reachable only from the keyboard is not reachable.
    ///
    /// The game's own reading of the other elements is suppressed while the trigger is held —
    /// see <see cref="Patches.ControllerLayerInput"/> — or every press would fire twice, once
    /// as a mod command and once as Interact or Dodge. Stick movement deliberately survives:
    /// the layer takes the buttons, not the character.
    /// </summary>
    internal static class ControllerLayer
    {
        /// <summary>
        /// How far the trigger must travel to count as held.
        ///
        /// Well past any resting drift, and well short of the full pull, because holding a
        /// trigger at full extension for the length of a menu is tiring and there is nothing
        /// else competing for the axis.
        /// </summary>
        private const float TriggerThreshold = 0.35f;

        private static readonly Dictionary<ModCommand, PadElement> Bindings =
            new Dictionary<ModCommand, PadElement>();

        /// <summary>Element name to live element id, resolved once per pad.</summary>
        private static readonly Dictionary<PadElement, int> Resolved =
            new Dictionary<PadElement, int>();

        private static Joystick _pad;
        private static bool _held;
        private static bool _announcedNoPad;

        internal static bool Enabled = true;

        /// <summary>
        /// True while the trigger is down, which is what the input patches suppress on.
        /// Requires the feature to be on and a pad to be present, so a keyboard-only session
        /// can never have its input suppressed by this.
        /// </summary>
        internal static bool Held => Enabled && _held;

        internal static void Bind(ModCommand command, PadElement element)
        {
            if (element == PadElement.LeftTrigger)
            {
                Plugin.Log.LogWarning(
                    $"[controller layer] {command} is bound to LeftTrigger, which is the layer " +
                    "modifier itself and cannot also be a command. Treating it as unbound.");
                element = PadElement.None;
            }

            Bindings[command] = element;
        }

        internal static void Reset() => Bindings.Clear();

        /// <summary>
        /// Driven from the plugin's Update, before the game reads its own input for the frame.
        /// Cheap when no pad is connected: one null check against a cached reference.
        /// </summary>
        internal static void Tick()
        {
            _held = false;
            if (!Enabled) return;

            var pad = ResolvePad();
            if (pad == null) return;

            _held = pad.GetAxisById(ElementId(PadElement.LeftTrigger)) >= TriggerThreshold;
            if (!_held) return;

            foreach (var pair in Bindings)
            {
                if (pair.Value == PadElement.None) continue;
                if (!pad.GetButtonDownById(ElementId(pair.Value))) continue;

                Plugin.Log.LogInfo(
                    $"[controller layer] command={pair.Key} element={pair.Value}");
                Dispatch(pair.Key);
            }
        }

        /// <summary>
        /// The analogue stick's own axis, so the input patch can hand back walking without the
        /// D-pad contribution the layer has claimed. False when there is no pad to read, in
        /// which case the caller must leave the game's own value alone rather than zero it.
        /// </summary>
        internal static bool TryReadStick(bool horizontal, out float value)
        {
            value = 0f;
            if (_pad == null || !_pad.isConnected) return false;

            value = _pad.GetAxisById(
                ElementId(horizontal ? PadElement.LeftStickX : PadElement.LeftStickY));
            return true;
        }

        private static void Dispatch(ModCommand command)
        {
            switch (command)
            {
                case ModCommand.NextTarget:
                    Navigation.Navigator.Cycle(1);
                    break;
                case ModCommand.PreviousTarget:
                    Navigation.Navigator.Cycle(-1);
                    break;
                case ModCommand.NextCategory:
                    Navigation.Navigator.CycleCategory(1);
                    break;
                case ModCommand.PreviousCategory:
                    Navigation.Navigator.CycleCategory(-1);
                    break;
                case ModCommand.ToggleGuidance:
                    Navigation.Navigator.ToggleTracking();
                    break;
                case ModCommand.AnnounceGuidance:
                    Navigation.Navigator.AnnounceGuidance();
                    break;
                case ModCommand.Autowalk:
                    Navigation.Autowalk.Toggle();
                    break;
                case ModCommand.Rescan:
                    Navigation.Navigator.Refresh();
                    break;
                case ModCommand.EnemyRoster:
                    Combat.EnemyRadar.AnnounceHostiles();
                    break;
                case ModCommand.CycleBeacon:
                    Combat.EnemyRadar.CycleBeaconTarget();
                    break;
                case ModCommand.CycleBeaconBack:
                    Combat.EnemyRadar.CycleBeaconTarget(-1);
                    break;
                case ModCommand.WhereAmI:
                    Status.PlayerStateAnnouncer.AnnounceCurrent();
                    break;
                case ModCommand.RepeatLast:
                    Speaker.RepeatHistory();
                    break;
                case ModCommand.Silence:
                    Speaker.Silence();
                    break;
                case ModCommand.Help:
                    Help.SpeakNext();
                    break;
                case ModCommand.SettingsMenu:
                    UI.ConfigMenu.Toggle();
                    break;
                case ModCommand.ReadPanel:
                    UI.PanelReader.ReadTopmostPanel();
                    break;
                case ModCommand.NearestValidCell:
                    Building.NearestValidCell.Announce();
                    break;
                case ModCommand.MarkLog:
                    Diagnostics.LogMarker.Mark();
                    break;
            }
        }

        /// <summary>
        /// The pad the layer reads, cached until it goes away.
        ///
        /// Deliberately the first connected joystick rather than a player's assigned one: the
        /// layer is the mod's own input surface and has nothing to do with which Rewired
        /// player the game has decided owns the pad, a thing that changes during co-op
        /// assignment and at controller reconnection.
        /// </summary>
        private static Joystick ResolvePad()
        {
            if (_pad != null && _pad.isConnected) return _pad;

            _pad = null;
            Resolved.Clear();

            if (!ReInput.isReady) return null;

            var joysticks = ReInput.controllers.Joysticks;
            if (joysticks == null || joysticks.Count == 0)
            {
                if (!_announcedNoPad)
                {
                    _announcedNoPad = true;
                    Plugin.Log.LogInfo(
                        "[controller layer] no joystick connected; layer idle. It will pick one " +
                        "up automatically if a pad is plugged in later.");
                }
                return null;
            }

            _pad = joysticks[0];
            _announcedNoPad = false;
            ResolveElements(_pad);
            return _pad;
        }

        /// <summary>
        /// Match the enum to this pad's live element ids by name, keeping the XInput id as the
        /// fallback. A pad that calls its face buttons something else still works if Rewired
        /// reports recognisable names, and the log says which route each one took so a pad
        /// that resolves badly can be diagnosed without guessing.
        /// </summary>
        private static void ResolveElements(Joystick pad)
        {
            var byName = new Dictionary<string, int>();
            var identifiers = pad.ElementIdentifiers;
            if (identifiers != null)
                foreach (var identifier in identifiers)
                    byName[Normalise(identifier.name)] = identifier.id;

            var fallbacks = 0;
            foreach (PadElement element in System.Enum.GetValues(typeof(PadElement)))
            {
                if (element == PadElement.None) continue;

                if (byName.TryGetValue(Normalise(element.ToString()), out var id))
                {
                    Resolved[element] = id;
                    continue;
                }

                Resolved[element] = (int)element;
                fallbacks++;
            }

            Plugin.Log.LogInfo(
                $"[controller layer] pad=\"{pad.name}\" hardware=\"{pad.hardwareName}\" " +
                $"elementsResolvedByName={Resolved.Count - fallbacks} byXInputId={fallbacks}");
        }

        private static int ElementId(PadElement element) =>
            Resolved.TryGetValue(element, out var id) ? id : (int)element;

        /// <summary>
        /// "Left Stick Button" and "LeftStickButton" are the same thing; "D-Pad Up" and
        /// "DPadUp" likewise. Compare with the separators removed rather than maintaining a
        /// table of the ways a vendor might punctuate a button name.
        /// </summary>
        private static string Normalise(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            var builder = new System.Text.StringBuilder(name.Length);
            foreach (var character in name)
                if (char.IsLetterOrDigit(character)) builder.Append(char.ToLowerInvariant(character));

            return builder.ToString();
        }
    }
}
