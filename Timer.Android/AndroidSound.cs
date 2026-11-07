using System;
using System.Threading.Tasks;
using Android.Media;

namespace Timer.Android;

/// <summary>Produces a short Android tone using the current media volume.</summary>
internal static class AndroidSound
{
    /// <summary>Plays one short beep and releases the native generator afterwards.</summary>
    public static async Task PlayAsync()
    {
        using var tone = new ToneGenerator(Stream.Music, 80);
        try
        {
            if (!tone.StartTone(Tone.PropBeep, 160))
                throw new InvalidOperationException("Android could not start the timer sound.");

            await Task.Delay(200).ConfigureAwait(false);
        }
        finally
        {
            tone.Release();
        }
    }
}
