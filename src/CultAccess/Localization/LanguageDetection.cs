using UnityEngine;

namespace CultAccess.Localization
{
    /// <summary>
    /// Adopts the game's language once the game has chosen it — and never before.
    ///
    /// Reading <c>LocalizationManager.CurrentLanguage</c> or <c>CurrentLanguageCode</c> is not
    /// a passive query. Both getters call <c>InitializeIfNeeded()</c>, which runs
    /// <c>AutoLoadGlobalParamManagers()</c>, <c>UpdateSources()</c> and
    /// <c>SelectStartupLanguage()</c>. Calling that from <c>Plugin.Awake</c> forces I2 to pick
    /// and apply a startup language before the game's own managers and saved settings exist,
    /// which broke startup after the plugin had already announced itself.
    ///
    /// <c>LocalizationManager.Sources</c> is a plain public static list, so its count can be
    /// read without triggering any of that. Waiting for it to be non-empty means the game has
    /// done its own initialization, and the language read afterwards is both correct and free.
    ///
    /// Until then the catalogue runs on compiled English, so the mod is never silent while it
    /// waits.
    /// </summary>
    internal static class LanguageDetection
    {
        /// <summary>Matches the other status polls; adopting a language is not urgent.</summary>
        private const float PollSeconds = 0.25f;

        private static float _nextPollAt;
        private static bool _adopted;

        internal static void Reset()
        {
            _adopted = false;
            _nextPollAt = 0f;
        }

        internal static void Tick(string pluginDirectory)
        {
            if (_adopted || Time.unscaledTime < _nextPollAt) return;
            _nextPollAt = Time.unscaledTime + PollSeconds;

            try
            {
                // Passive: a field read, not the property that would force initialization.
                var sources = I2.Loc.LocalizationManager.Sources;
                if (sources == null || sources.Count == 0) return;

                var name = I2.Loc.LocalizationManager.CurrentLanguage;
                if (string.IsNullOrEmpty(name)) return;

                var code = I2.Loc.LocalizationManager.CurrentLanguageCode;
                _adopted = true;

                Strings.Initialize(pluginDirectory, code, name);
                Plugin.Log.LogInfo(
                    $"Localization adopted the game language: name=\"{name}\" code=\"{code}\" " +
                    $"catalogue={Strings.Language}");

                // Only now: the icon vocabulary is read out of the game's own name table, so
                // building it earlier would capture whatever language I2 had not yet applied.
                SpriteVocabulary.Build();
            }
            catch (System.Exception e)
            {
                // English is already loaded, so a failure here costs wording, not speech.
                _adopted = true;
                Plugin.Log.LogWarning(
                    $"Could not adopt the game language; staying on English: {e.Message}");
            }
        }
    }
}
