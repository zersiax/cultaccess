using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CultAccess.Speech
{
    /// <summary>
    /// Loads native screen-reader libraries by absolute path before any DllImport binds.
    /// Windows caches loaded modules by base name, so a later implicit load of
    /// "Tolk.dll" resolves to the module we pinned here rather than searching PATH.
    /// Without this, the DLLs would have to sit in the game root next to the exe.
    /// </summary>
    internal static class NativeLoader
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string lpLibFileName);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectoryW(string lpPathName);

        private static bool _searchPathAdded;

        /// <summary>
        /// Pin <paramref name="fileName"/> from <paramref name="dir"/> into the process.
        /// Returns false if the file is absent or the load failed (wrong architecture,
        /// missing transitive dependency).
        /// </summary>
        public static bool TryPreload(string dir, string fileName)
        {
            if (string.IsNullOrEmpty(dir)) return false;

            var full = Path.Combine(dir, fileName);
            if (!File.Exists(full)) return false;

            // Tolk loads its own screen-reader adapter DLLs by name from the search
            // path, so the directory itself has to become searchable too.
            if (!_searchPathAdded)
            {
                SetDllDirectoryW(dir);
                _searchPathAdded = true;
            }

            var handle = LoadLibraryW(full);
            if (handle == IntPtr.Zero)
            {
                Plugin.Log.LogWarning(
                    $"Found {fileName} but LoadLibrary failed (win32 error {Marshal.GetLastWin32Error()}). " +
                    "Is it the 64-bit build?");
                return false;
            }

            Plugin.Log.LogInfo($"Loaded native library {full}");
            return true;
        }
    }
}
