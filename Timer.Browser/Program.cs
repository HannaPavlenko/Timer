using System;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Timer.Core;
using Timer.ViewModels;

namespace Timer.Browser;

internal static partial class Program
{
    private const string HistoryKey = "Stilltime:history";
    private const string SoundKey = "Stilltime:sound";

    private static Task Main()
    {
        App.ViewModelFactory = CreateViewModel;
        App.SoundPlayback = new SoundPlayback(sound => PlayWave(Convert.ToBase64String(TimerSoundAudio.CreateWave(sound))));

        return AppBuilder.Configure<App>()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    private static MainViewModel CreateViewModel()
    {
        TimerHistoryStore history = new(() => Read(HistoryKey), json => Write(HistoryKey, json));
        SoundSettingsStore sound = new(() => Read(SoundKey), json => Write(SoundKey, json));
        return new MainViewModel(history, sound);
    }

    private static string? Read(string key)
    {
        try
        {
            return ReadStorage(key);
        }
        catch (JSException exception)
        {
            throw new IOException("Browser storage could not be read.", exception);
        }
    }

    private static void Write(string key, string json)
    {
        try
        {
            WriteStorage(key, json);
        }
        catch (JSException exception)
        {
            throw new IOException("Browser storage could not be updated.", exception);
        }
    }

    [JSImport("readStorage", "stilltime")]
    private static partial string? ReadStorage(string key);

    [JSImport("writeStorage", "stilltime")]
    private static partial void WriteStorage(string key, string json);

    [JSImport("playWave", "stilltime")]
    private static partial Task PlayWave(string wave);
}
