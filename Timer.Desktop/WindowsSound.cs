using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Timer.Core;

namespace Timer.Desktop;

/// <summary>Plays portable timer cues on the Windows audio device.</summary>
internal static class WindowsSound
{
    /// <summary>Plays the complete cue on a worker thread.</summary>
    public static Task PlayAsync(TimerSound sound) => Task.Run(() =>
    {
        if (OperatingSystem.IsWindows() &&
            !PlaySound(TimerSoundAudio.CreateWave(sound), IntPtr.Zero, 0x0004 | 0x0002))
            throw new InvalidOperationException("Windows could not play the timer sound.");
    });

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(byte[] data, IntPtr module, uint flags);
}
