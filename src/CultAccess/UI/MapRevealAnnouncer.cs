using CultAccess.Speech;
using CultAccess.Util;
using HarmonyLib;
using Lamb.UI;

namespace CultAccess.UI
{
    /// <summary>
    /// Says which place has just been revealed on the world map.
    ///
    /// `UnlockMapLocation.Play` calls `DataManager.DiscoverLocation`, loads the world map and
    /// opens it on the new place with a reveal flourish — clouds parting over an icon — then
    /// closes again on its own. It is a cutscene, not a menu: nothing is focused, there is no
    /// list to step through, and the only text on screen is the location's own label riding an
    /// animation.
    ///
    /// So the generic reader had nothing to describe and the player heard the window's title,
    /// "Cult of the Lamb", and then silence. The whole event — a new region opening up, which
    /// is one of the few things in this game that changes what you can do next — passed
    /// unannounced. The objective that follows a moment later says where to go but never says
    /// anywhere has been unlocked, and it is not always present.
    ///
    /// Patched at `UIWorldMapMenuController.Show` rather than at `UnlockMapLocation`, because
    /// Show is what actually receives the location and is also used for a re-reveal, which is
    /// a different sentence: being shown somewhere again is not a discovery.
    /// </summary>
    [HarmonyPatch]
    internal static class MapRevealAnnouncer
    {
        internal static bool Enabled = true;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIWorldMapMenuController), nameof(UIWorldMapMenuController.Show))]
        private static void AfterShow(
            UIWorldMapMenuController __instance, FollowerLocation revealLocation, bool reReveal)
        {
            if (!Enabled || revealLocation == FollowerLocation.None) return;

            try
            {
                var name = Name(__instance, revealLocation);

                Plugin.Log.LogInfo(
                    $"[map reveal] location={revealLocation} reReveal={reReveal} name=\"{name}\"");

                // Queued rather than interrupting: this arrives on the heels of the
                // conversation that caused it, and cutting off the last line of that to say
                // this would trade one missing announcement for another.
                Speaker.Say(
                    reReveal
                        ? $"Showing {name} on the world map."
                        : $"New location revealed on the world map: {name}.",
                    SpeechPriority.Queued);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[map reveal] could not describe the reveal: {e.Message}");
            }
        }

        /// <summary>
        /// The revealed place's own localised name, found the way the controller finds it —
        /// by matching `WorldMapIcon.Location` against the location being revealed.
        ///
        /// Falls back to the humanised enum rather than to silence: a mechanical name still
        /// tells the player something opened up, and which of several it was.
        /// </summary>
        private static string Name(UIWorldMapMenuController controller, FollowerLocation location)
        {
            var icons = IconsField == null ? null : IconsField(controller);
            if (icons != null)
                foreach (var icon in icons)
                {
                    if (icon == null || icon.Location != location) continue;

                    try
                    {
                        var localised = icon.GetLocalisedLocation();
                        if (RichText.IsUsableLocalization(localised, icon.LocationTerm))
                            return RichText.Clean(localised);
                    }
                    catch (System.Exception)
                    {
                        // Fall through to the mechanical name rather than losing the event.
                    }

                    break;
                }

            return RichText.HumaniseKey(location.ToString());
        }

        private static readonly AccessTools.FieldRef<UIWorldMapMenuController, WorldMapIcon[]>
            IconsField = BuildIconsAccessor();

        private static AccessTools.FieldRef<UIWorldMapMenuController, WorldMapIcon[]>
            BuildIconsAccessor()
        {
            try
            {
                var field = AccessTools.Field(typeof(UIWorldMapMenuController), "_locations");
                if (field != null)
                    return AccessTools
                        .FieldRefAccess<UIWorldMapMenuController, WorldMapIcon[]>(field);

                Plugin.Log.LogWarning(
                    "[map reveal] UIWorldMapMenuController has no _locations field; reveals " +
                    "will be named from the location enum rather than the game's own wording.");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[map reveal] could not bind _locations: {e.Message}");
            }

            return null;
        }
    }
}
