using CultAccess.Speech;
using HarmonyLib;
using Lamb.UI;
using UnityEngine.UI;

namespace CultAccess.UI
{
    /// <summary>
    /// Sliders and horizontal selectors intentionally return themselves from directional
    /// navigation. UINavigator therefore does not call NavigateTo again, so an ordinary
    /// focus-change hook cannot observe their new value. Toggles likewise keep focus after
    /// confirmation. Announce only when the underlying value actually changed.
    /// </summary>
    [HarmonyPatch]
    internal static class ControlValueAnnouncer
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MMSelectable_Slider), nameof(MMSelectable_Slider.TryNavigateLeft))]
        private static void BeforeSliderLeft(MMSelectable_Slider __instance, out float __state)
            => __state = SliderValue(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MMSelectable_Slider), nameof(MMSelectable_Slider.TryNavigateLeft))]
        private static void AfterSliderLeft(MMSelectable_Slider __instance, float __state)
            => AnnounceSliderIfChanged(__instance, __state);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MMSelectable_Slider), nameof(MMSelectable_Slider.TryNavigateRight))]
        private static void BeforeSliderRight(MMSelectable_Slider __instance, out float __state)
            => __state = SliderValue(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MMSelectable_Slider), nameof(MMSelectable_Slider.TryNavigateRight))]
        private static void AfterSliderRight(MMSelectable_Slider __instance, float __state)
            => AnnounceSliderIfChanged(__instance, __state);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MMSelectable_HorizontalSelector), nameof(MMSelectable_HorizontalSelector.TryNavigateLeft))]
        private static void BeforeSelectorLeft(MMSelectable_HorizontalSelector __instance, out int __state)
            => __state = Selection(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MMSelectable_HorizontalSelector), nameof(MMSelectable_HorizontalSelector.TryNavigateLeft))]
        private static void AfterSelectorLeft(MMSelectable_HorizontalSelector __instance, int __state)
            => AnnounceSelectorIfChanged(__instance, __state);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MMSelectable_HorizontalSelector), nameof(MMSelectable_HorizontalSelector.TryNavigateRight))]
        private static void BeforeSelectorRight(MMSelectable_HorizontalSelector __instance, out int __state)
            => __state = Selection(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MMSelectable_HorizontalSelector), nameof(MMSelectable_HorizontalSelector.TryNavigateRight))]
        private static void AfterSelectorRight(MMSelectable_HorizontalSelector __instance, int __state)
            => AnnounceSelectorIfChanged(__instance, __state);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MMSelectable_Toggle), nameof(MMSelectable_Toggle.TryPerformConfirmAction))]
        private static void BeforeToggle(MMSelectable_Toggle __instance, out bool __state)
            => __state = ToggleValue(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MMSelectable_Toggle), nameof(MMSelectable_Toggle.TryPerformConfirmAction))]
        private static void AfterToggle(MMSelectable_Toggle __instance, bool __state)
        {
            if (__instance?.Toggle == null || __instance.Toggle.Value == __state) return;
            Announce(__instance);
        }

        private static float SliderValue(MMSelectable_Slider selectable)
            => selectable?.Slider == null ? float.NaN : selectable.Slider.value;

        private static int Selection(MMSelectable_HorizontalSelector selectable)
            => selectable?.HorizontalSelector == null ? -1 : selectable.HorizontalSelector.CurrentSelection;

        private static bool ToggleValue(MMSelectable_Toggle selectable)
            => selectable?.Toggle != null && selectable.Toggle.Value;

        private static void AnnounceSliderIfChanged(MMSelectable_Slider selectable, float oldValue)
        {
            if (selectable?.Slider == null || float.IsNaN(oldValue)) return;
            if (selectable.Slider.value.Equals(oldValue)) return;
            Announce(selectable);
        }

        private static void AnnounceSelectorIfChanged(MMSelectable_HorizontalSelector selectable, int oldSelection)
        {
            if (selectable?.HorizontalSelector == null || oldSelection < 0) return;
            if (selectable.HorizontalSelector.CurrentSelection == oldSelection) return;
            Announce(selectable);
        }

        private static void Announce(Selectable selectable)
        {
            if (!Plugin.SpeechEnabled.Value || selectable == null) return;
            var description = SelectableDescriber.Describe(selectable);
            Plugin.Log.LogInfo($"[control change] {selectable.GetType().Name}: \"{description}\"");
            Speaker.Say(description, SpeechPriority.Now);
        }
    }
}
