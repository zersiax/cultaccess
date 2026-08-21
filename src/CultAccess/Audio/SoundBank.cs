using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FMODUnity;

namespace CultAccess.Audio
{
    /// <summary>
    /// Resolves every sound the mod plays, preferring a file the player supplied over the
    /// generated default.
    ///
    /// The same two-layer shape the string catalogue uses, for the same reason. A generated
    /// waveform is the guarantee: it cannot be missing, cannot be corrupt, needs no licence
    /// and keeps the mod a single DLL with no bundled assets. A file in <c>sounds/</c> beside
    /// the plugin overrides it, so a player who finds a cue unclear, fatiguing or too similar
    /// to something else can replace that one sound without touching anything else and
    /// without a rebuild.
    ///
    /// That matters more here than it looks. Which sounds are legible is genuinely personal —
    /// it depends on the screen reader, the headphones, the hearing, and what else the player
    /// has learned to listen for. Shipping one fixed set and calling it accessible would be
    /// the same mistake as shipping one fixed language.
    ///
    /// Nothing is shipped in <c>sounds/</c>. The folder is documented and empty, so there is
    /// no third-party audio to licence and no question about what may be redistributed.
    /// </summary>
    internal static class SoundBank
    {
        private const string FolderName = "sounds";

        /// <summary>Extensions tried in order, first match wins.</summary>
        private static readonly string[] Extensions = { ".wav", ".ogg", ".mp3" };

        private static readonly Dictionary<string, FMOD.Sound> Loaded =
            new Dictionary<string, FMOD.Sound>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Failed =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Where a player drops replacement sounds.</summary>
        internal static string Folder =>
            Path.Combine(Plugin.PluginDir ?? string.Empty, FolderName);

        /// <summary>
        /// Get a sound by name, loading it once and caching it. <paramref name="generate"/>
        /// supplies the built-in waveform and is only called when no file is present.
        /// </summary>
        internal static bool TryGet(
            string key,
            bool looping,
            Func<float[]> generate,
            out FMOD.Sound sound,
            string variant = null)
        {
            // Cached per looping mode, not per name. Whether a sound loops is fixed when FMOD
            // creates it, so one cache entry shared between a sustained user and a one-shot
            // user hands the second one whatever the first asked for - and a one-shot handed a
            // looping sound never stops. The wall ring and its preview are exactly that pair.
            var cacheKey = looping ? key + "#loop" : key + "#once";

            // A variant distinguishes two generated forms of the same named sound - the wall
            // ring's notes differ by scheme. It is not part of the file name, so a file the
            // player supplied is found and reused whatever the variant.
            if (!string.IsNullOrEmpty(variant)) cacheKey += "#" + variant;

            if (Loaded.TryGetValue(cacheKey, out sound)) return true;

            sound = default;
            if (Failed.Contains(cacheKey)) return false;

            if (TryLoadFile(key, looping, out sound) ||
                TryGenerate(key, looping, generate, out sound))
            {
                Loaded.Add(cacheKey, sound);
                return true;
            }

            Failed.Add(cacheKey);
            return false;
        }

        internal static void Shutdown()
        {
            foreach (var sound in Loaded.Values)
                sound.release();

            Loaded.Clear();
            Failed.Clear();
        }

        private static bool TryLoadFile(string key, bool looping, out FMOD.Sound sound)
        {
            sound = default;

            var folder = Folder;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return false;

            foreach (var extension in Extensions)
            {
                var path = Path.Combine(folder, key + extension);
                if (!File.Exists(path)) continue;

                try
                {
                    var mode = FMOD.MODE.CREATESAMPLE | FMOD.MODE._2D |
                               (looping ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF);
                    var result = RuntimeManager.CoreSystem.createSound(path, mode, out sound);
                    if (result != FMOD.RESULT.OK)
                    {
                        // A replacement the player deliberately supplied and that does not
                        // work must say so by name. Falling back silently would leave them
                        // convinced their file is in use when it never loaded.
                        Plugin.Log.LogWarning(
                            $"[sound bank] could not load \"{path}\": {result}. " +
                            "Using the built-in sound instead.");
                        return false;
                    }

                    Plugin.Log.LogInfo($"[sound bank] {key} loaded from {path}");
                    return true;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning(
                        $"[sound bank] could not load \"{path}\": {e.Message}. " +
                        "Using the built-in sound instead.");
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Build the sound as raw PCM in FMOD's own memory, from the generated waveform.
        /// </summary>
        private static bool TryGenerate(
            string key,
            bool looping,
            Func<float[]> generate,
            out FMOD.Sound sound)
        {
            sound = default;
            if (generate == null) return false;

            try
            {
                var samples = generate();
                if (samples == null || samples.Length == 0)
                {
                    Plugin.Log.LogError($"[sound bank] {key} generated no audio.");
                    return false;
                }

                var bytes = new byte[samples.Length * sizeof(float)];
                Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);

                var info = new FMOD.CREATESOUNDEXINFO
                {
                    cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)),
                    length = (uint)bytes.Length,
                    numchannels = 1,
                    defaultfrequency = Waveform.SampleRate,
                    format = FMOD.SOUND_FORMAT.PCMFLOAT,
                };

                var mode = FMOD.MODE.OPENMEMORY | FMOD.MODE.OPENRAW | FMOD.MODE.CREATESAMPLE |
                           FMOD.MODE._2D |
                           (looping ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF);

                var result = RuntimeManager.CoreSystem.createSound(bytes, mode, ref info, out sound);
                if (result != FMOD.RESULT.OK)
                {
                    Plugin.Log.LogError($"[sound bank] {key} createSound failed: {result}");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[sound bank] {key} creation failed: {e}");
                return false;
            }
        }
    }
}
