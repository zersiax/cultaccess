using System.Collections.Generic;
using System.Reflection;
using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;

namespace CultAccess.Navigation
{
    /// <summary>Semantic state for a weapon, curse, or relic selection podium.</summary>
    internal sealed class WeaponPodiumSnapshot
    {
        public bool ItemReady;
        public bool Available;
        public bool Taken;
        public bool Activated;
        public string ItemName;
        public string ItemKind;
        public string Description;
        public int Level;
        public EquipmentType EquipmentType;
        public RelicType RelicType;
    }

    /// <summary>
    /// Weapon podium labels are intentionally empty until the Interactor first assigns the
    /// podium as TempInteraction. Their concrete item fields are already populated, so an
    /// empty scan-time label is not an unavailable state and must not hide what will equip.
    /// </summary>
    internal static class WeaponPodiumTarget
    {
        private static readonly FieldInfo WeaponLevelField = AccessTools.Field(
            typeof(Interaction_WeaponSelectionPodium), "WeaponLevel");
        private static readonly FieldInfo ActivatedField = AccessTools.Field(
            typeof(Interaction_WeaponSelectionPodium), "activated");
        private static readonly HashSet<int> AnnouncedEquips = new HashSet<int>();
        private static readonly HashSet<string> ReportedFailures = new HashSet<string>();

        public static WeaponPodiumSnapshot Inspect(
            Interaction_WeaponSelectionPodium podium,
            InteractionAvailability interactionState = null)
        {
            var snapshot = new WeaponPodiumSnapshot
            {
                ItemName = "unknown equipment",
                ItemKind = "equipment",
            };
            if (podium == null) return snapshot;

            interactionState = interactionState ?? InteractionAvailability.Inspect(podium);
            snapshot.Taken = podium.WeaponTaken;
            snapshot.Activated = ReadBool(ActivatedField, podium, "activated");
            snapshot.Level = ReadInt(WeaponLevelField, podium, "WeaponLevel");
            snapshot.EquipmentType = podium.TypeOfWeapon;
            snapshot.RelicType = podium.TypeOfRelic;

            try
            {
                if (snapshot.RelicType != RelicType.None)
                {
                    snapshot.ItemReady = true;
                    snapshot.ItemKind = "relic";
                    snapshot.ItemName = CleanOrFallback(
                        RelicData.GetTitleLocalisation(snapshot.RelicType),
                        snapshot.RelicType.ToString());
                    snapshot.Description = RichText.Clean(
                        RelicData.GetDescriptionLocalisation(snapshot.RelicType));
                }
                else if (snapshot.EquipmentType != EquipmentType.None)
                {
                    var data = EquipmentManager.GetEquipmentData(snapshot.EquipmentType);
                    snapshot.ItemReady = data != null;
                    snapshot.ItemKind = EquipmentManager.GetWeaponData(snapshot.EquipmentType) != null
                        ? "weapon"
                        : EquipmentManager.GetCurseData(snapshot.EquipmentType) != null
                            ? "curse"
                            : "equipment";
                    snapshot.ItemName = data == null
                        ? RichText.Humanise(snapshot.EquipmentType.ToString())
                        : CleanOrFallback(
                            data.GetLocalisedTitle(), snapshot.EquipmentType.ToString());
                    snapshot.Description = data == null
                        ? null
                        : RichText.Clean(data.GetLocalisedDescription());
                }
            }
            catch (System.Exception e)
            {
                ReportFailure($"Could not inspect weapon podium item: {e.Message}");
                snapshot.ItemReady = snapshot.EquipmentType != EquipmentType.None ||
                                     snapshot.RelicType != RelicType.None;
                if (snapshot.EquipmentType != EquipmentType.None)
                    snapshot.ItemName = RichText.Humanise(snapshot.EquipmentType.ToString());
                else if (snapshot.RelicType != RelicType.None)
                    snapshot.ItemName = RichText.Humanise(snapshot.RelicType.ToString());
            }

            snapshot.Available = interactionState.ObjectActive &&
                                 interactionState.ComponentEnabled &&
                                 interactionState.Registered &&
                                 podium.Interactable &&
                                 !snapshot.Taken &&
                                 !snapshot.Activated &&
                                 snapshot.ItemReady;
            return snapshot;
        }

        public static string Name(
            Interaction_WeaponSelectionPodium podium,
            InteractionAvailability interactionState = null)
        {
            var state = Inspect(podium, interactionState);
            return $"{state.ItemName}, {state.ItemKind} podium";
        }

        public static string Describe(
            string name,
            WeaponPodiumSnapshot state)
        {
            if (state == null) return $"{name}, currently unavailable";
            if (state.Available) return $"{name}, ready to equip with Interact";
            if (state.Taken || state.Activated) return $"{name}, already selected";
            return $"{name}, currently unavailable";
        }

        public static string Prompt(
            Interaction_WeaponSelectionPodium podium,
            PlayerFarming player)
        {
            var state = Inspect(podium);
            var level = state.Level > 0 ? $", level {state.Level}" : string.Empty;
            var stats = string.Empty;

            if (state.ItemKind == "weapon" && player?.playerWeapon != null)
            {
                try
                {
                    var damage = player.playerWeapon.GetAverageWeaponDamage(
                        state.EquipmentType, state.Level);
                    var speed = player.playerWeapon.GetWeaponSpeed(state.EquipmentType);
                    stats = $" Damage {damage:0.##}, speed {speed:0.##}.";
                }
                catch (System.Exception e)
                {
                    ReportFailure($"Could not read weapon podium stats: {e.Message}");
                }
            }

            var description = string.IsNullOrEmpty(state.Description)
                ? string.Empty
                : $" {state.Description}";
            return $"Equip {state.ItemName}, {state.ItemKind}{level}.{stats}{description}";
        }

        public static string Arrival(
            string name,
            WeaponPodiumSnapshot state)
        {
            return state != null && state.Available
                ? $"Arrived at {name}. Press Interact to equip it."
                : $"Reached {name}. It is currently unavailable.";
        }

        internal static void AnnounceEquipped(
            Interaction_WeaponSelectionPodium podium,
            PlayerFarming player)
        {
            if (podium == null || !AnnouncedEquips.Add(podium.GetInstanceID())) return;

            var state = Inspect(podium);
            var level = state.Level > 0 ? $", level {state.Level}" : string.Empty;
            Plugin.Log.LogInfo(
                $"[weapon podium] equipped name=\"{state.ItemName}\" kind={state.ItemKind} " +
                $"level={state.Level} equipment={state.EquipmentType} relic={state.RelicType} " +
                $"player={(player == null ? "none" : player.name)}");
            Speaker.Say($"Equipped {state.ItemName}, {state.ItemKind}{level}.", SpeechPriority.Now);
            Navigator.RefreshAfterWorldChange();
        }

        internal static void ResetAnnouncement(Interaction_WeaponSelectionPodium podium)
        {
            if (podium != null) AnnouncedEquips.Remove(podium.GetInstanceID());
        }

        internal static void Shutdown()
        {
            AnnouncedEquips.Clear();
            ReportedFailures.Clear();
        }

        private static string CleanOrFallback(string value, string fallback)
        {
            var clean = RichText.Clean(value);
            return clean.Length > 0 ? clean : RichText.Humanise(fallback);
        }

        private static int ReadInt(FieldInfo field, object instance, string name)
        {
            if (field == null)
            {
                ReportFailure($"Weapon podium field {name} was not found.");
                return 0;
            }

            try
            {
                return (int)field.GetValue(instance);
            }
            catch (System.Exception e)
            {
                ReportFailure($"Could not read weapon podium field {name}: {e.Message}");
                return 0;
            }
        }

        private static bool ReadBool(FieldInfo field, object instance, string name)
        {
            if (field == null)
            {
                ReportFailure($"Weapon podium field {name} was not found.");
                return false;
            }

            try
            {
                return (bool)field.GetValue(instance);
            }
            catch (System.Exception e)
            {
                ReportFailure($"Could not read weapon podium field {name}: {e.Message}");
                return false;
            }
        }

        private static void ReportFailure(string message)
        {
            if (ReportedFailures.Add(message)) Plugin.Log.LogWarning(message);
        }
    }

    [HarmonyPatch(typeof(Interaction_WeaponSelectionPodium),
        nameof(Interaction_WeaponSelectionPodium.SetWeaponInactive))]
    internal static class WeaponPodiumWeaponEquippedPatch
    {
        [HarmonyPostfix]
        private static void AfterWeaponEquipped(Interaction_WeaponSelectionPodium __instance)
        {
            WeaponPodiumTarget.AnnounceEquipped(__instance, PlayerFarming.Instance);
        }
    }

    [HarmonyPatch(typeof(Interaction_WeaponSelectionPodium), "OnCurseChanged")]
    internal static class WeaponPodiumCurseEquippedPatch
    {
        [HarmonyPostfix]
        private static void AfterCurseChanged(
            Interaction_WeaponSelectionPodium __instance,
            EquipmentType curse,
            PlayerFarming playerfarming)
        {
            if (__instance.WeaponTaken && curse == __instance.TypeOfWeapon)
                WeaponPodiumTarget.AnnounceEquipped(__instance, playerfarming);
        }
    }

    [HarmonyPatch(typeof(Interaction_WeaponSelectionPodium), "OnRelicChanged")]
    internal static class WeaponPodiumRelicEquippedPatch
    {
        [HarmonyPostfix]
        private static void AfterRelicChanged(
            Interaction_WeaponSelectionPodium __instance,
            RelicData relic,
            PlayerFarming playerfarming)
        {
            if (relic != null && relic.RelicType == __instance.TypeOfRelic &&
                WeaponPodiumTarget.Inspect(__instance).Activated)
                WeaponPodiumTarget.AnnounceEquipped(__instance, playerfarming);
        }
    }

    [HarmonyPatch(typeof(Interaction_WeaponSelectionPodium),
        nameof(Interaction_WeaponSelectionPodium.ResetRandom))]
    internal static class WeaponPodiumResetPatch
    {
        [HarmonyPrefix]
        private static void BeforeReset(Interaction_WeaponSelectionPodium __instance)
        {
            WeaponPodiumTarget.ResetAnnouncement(__instance);
        }
    }
}
