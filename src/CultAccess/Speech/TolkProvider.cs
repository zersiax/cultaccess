using System;
using System.Runtime.InteropServices;

namespace CultAccess.Speech
{
    /// <summary>
    /// Preferred provider. Tolk abstracts over NVDA, JAWS, ZoomText, SuperNova,
    /// System Access and Window-Eyes, with an optional SAPI fallback of its own.
    /// Requires Tolk.dll (64-bit) plus its adapter DLLs in the native directory.
    /// </summary>
    internal sealed class TolkProvider : ISpeechProvider
    {
        private const string Dll = "Tolk.dll";
        private bool _loaded;

        public string Name { get; private set; } = "Tolk";

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Load();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_IsLoaded();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Unload();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_HasSpeech();

        // Tolk_Output would drive speech and braille together, but then the braille config
        // toggle could not work. Drive the two channels separately instead.
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_Speak(string str, [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_Braille(string str);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_HasBraille();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool Tolk_Silence();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_TrySAPI([MarshalAs(UnmanagedType.I1)] bool trySAPI);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Tolk_DetectScreenReader();

        public bool TryInitialize(string nativeDir)
        {
            if (!NativeLoader.TryPreload(nativeDir, Dll)) return false;

            try
            {
                Tolk_TrySAPI(true);
                Tolk_Load();
                if (!Tolk_IsLoaded() || !Tolk_HasSpeech()) return false;

                var namePtr = Tolk_DetectScreenReader();
                var detected = namePtr == IntPtr.Zero ? null : Marshal.PtrToStringUni(namePtr);
                Name = string.IsNullOrEmpty(detected) ? "Tolk" : $"Tolk ({detected})";

                _loaded = true;
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Tolk initialisation failed: {e.Message}");
                return false;
            }
        }

        public void Speak(string text, bool interrupt)
        {
            if (!_loaded) return;
            Tolk_Speak(text, interrupt);
        }

        public bool SupportsBraille
        {
            get
            {
                if (!_loaded) return false;
                try { return Tolk_HasBraille(); }
                catch { return false; }
            }
        }

        public void Braille(string text)
        {
            if (!_loaded) return;
            Tolk_Braille(text);
        }

        public void Silence()
        {
            if (!_loaded) return;
            Tolk_Silence();
        }

        public void Dispose()
        {
            if (!_loaded) return;
            _loaded = false;
            try { Tolk_Unload(); } catch { /* shutting down anyway */ }
        }
    }
}
