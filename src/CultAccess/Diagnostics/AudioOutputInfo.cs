using System;
using FMODUnity;

namespace CultAccess.Diagnostics
{
    /// <summary>
    /// Records which device the game's audio is going to, and how much delay FMOD itself
    /// adds. Log-only, never spoken, once per run.
    ///
    /// Written because a report of lag during weapon attacks turned out to be worth splitting
    /// in two. The frame-budget numbers measure wall-clock time inside the mod's own code, so
    /// they can only ever see *compute* stalls. They cannot see the other half of the chain:
    /// how long a sound takes to reach the player's ears after the game has finished with it.
    ///
    /// **That half can be much the larger one.** A wireless headset adds output delay
    /// downstream of the game entirely — in the operating system, the driver and the headset —
    /// and the game has no way to observe it. For a player whose only feedback channel is
    /// audio, that delay is subjectively indistinguishable from the game responding slowly,
    /// which is precisely how it gets reported.
    ///
    /// What is written here is what can honestly be known: the device name, the mixer format,
    /// and FMOD's own buffer latency. **The wireless link's own latency is not included and
    /// cannot be measured from here.** The value of the line is that it names the output path,
    /// so a future timing report can be read in the light of what it was played through.
    /// </summary>
    internal static class AudioOutputInfo
    {
        private const int NameLength = 256;

        internal static bool Enabled = true;

        private static bool _done;

        internal static void RunOnce()
        {
            if (_done || !Enabled) return;
            _done = true;

            try
            {
                var system = RuntimeManager.CoreSystem;

                if (system.getDriver(out var driver) != FMOD.RESULT.OK) return;

                if (system.getDriverInfo(
                        driver, out var name, NameLength, out _, out var rate,
                        out var speakerMode, out var channels) == FMOD.RESULT.OK)
                    Plugin.Log.LogInfo(
                        $"[audio out] device=\"{name}\" rate={rate} mode={speakerMode} " +
                        $"channels={channels}");

                if (system.getDSPBufferSize(out var bufferLength, out var buffers) !=
                    FMOD.RESULT.OK)
                    return;

                if (system.getSoftwareFormat(out var mixRate, out _, out _) != FMOD.RESULT.OK)
                    return;

                // FMOD's own contribution only. Everything after it — the OS mixer, the
                // driver, a wireless link — is invisible from here and is usually larger.
                var mixerLatencyMs = mixRate > 0
                    ? bufferLength * buffers * 1000.0 / mixRate
                    : 0.0;

                Plugin.Log.LogInfo(
                    $"[audio out] mixerBuffer={bufferLength}x{buffers} " +
                    $"mixerLatency={mixerLatencyMs:0.0}ms " +
                    "(this is FMOD's share only; any wireless headset adds its own delay " +
                    "downstream, which nothing here can see)");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[audio out] could not read the output device: {e.Message}");
            }
        }
    }
}
