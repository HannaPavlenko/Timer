using System;
using System.Threading.Tasks;

namespace Timer.Desktop;

/// <summary>Produces a short tone on the default Windows audio device without blocking the UI.</summary>
internal static class WindowsSound
{
    /// <summary>Plays a 160-millisecond completion tone on Windows.</summary>
    public static Task PlayAsync() => Task.Run(() =>
    {
        if (OperatingSystem.IsWindows())
            Console.Beep(880, 160);
    });
}
