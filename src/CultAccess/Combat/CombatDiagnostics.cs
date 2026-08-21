namespace CultAccess.Combat
{
    internal static class CombatDiagnostics
    {
        internal static bool Enabled = true;

        internal static void Info(string message)
        {
            if (Enabled) Plugin.Log.LogInfo(message);
        }
    }
}
