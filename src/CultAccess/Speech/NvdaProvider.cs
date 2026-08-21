using System;
using System.Runtime.InteropServices;

namespace CultAccess.Speech
{
    /// <summary>
    /// Talks to NVDA directly via its controller client. Used when Tolk is not present
    /// but nvdaControllerClient64.dll is. Only works while NVDA is actually running.
    /// </summary>
    internal sealed class NvdaProvider : ISpeechProvider
    {
        private const string Dll = "nvdaControllerClient64.dll";
        private bool _loaded;

        public string Name => "NVDA controller client";

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_testIfRunning();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int nvdaController_speakText(string text);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int nvdaController_cancelSpeech();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int nvdaController_brailleMessage(string message);

        public bool TryInitialize(string nativeDir)
        {
            if (!NativeLoader.TryPreload(nativeDir, Dll)) return false;

            try
            {
                if (nvdaController_testIfRunning() != 0)
                {
                    Plugin.Log.LogInfo("nvdaControllerClient64.dll present but NVDA is not running.");
                    return false;
                }

                _loaded = true;
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"NVDA controller client initialisation failed: {e.Message}");
                return false;
            }
        }

        public void Speak(string text, bool interrupt)
        {
            if (!_loaded) return;

            // The controller client has no interrupt flag; cancel first to emulate one.
            if (interrupt) nvdaController_cancelSpeech();
            nvdaController_speakText(text);
        }

        public bool SupportsBraille => _loaded;

        public void Braille(string text)
        {
            if (!_loaded) return;

            // NVDA decides whether a display is attached; if none is, this is a no-op.
            var result = nvdaController_brailleMessage(text);
            if (result != 0)
                Plugin.Log.LogDebug($"nvdaController_brailleMessage returned {result}");
        }

        public void Silence()
        {
            if (!_loaded) return;
            nvdaController_cancelSpeech();
        }

        public void Dispose() => _loaded = false;
    }
}
