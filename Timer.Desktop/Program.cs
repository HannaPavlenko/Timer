using System;
using Avalonia;
using Timer.Core;

namespace Timer.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (OperatingSystem.IsWindows())
        {
            SingleInstanceApplication.TryRun(() => RunApplication(args));
            return;
        }

        RunApplication(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void RunApplication(string[] args)
    {
        App.SoundPlayback = new SoundPlayback(WindowsSound.PlayAsync);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
}
