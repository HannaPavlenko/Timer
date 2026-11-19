using System;
using System.IO;
using System.Threading.Tasks;
using Android.Content;
using Android.Media;
using Timer.Core;

namespace Timer.Android;

/// <summary>Plays timer cues through the Android media audio device.</summary>
internal static class AndroidSound
{
    /// <summary>Plays a WAV cue and releases the player and temporary file afterwards.</summary>
    public static async Task PlayAsync(TimerSound sound)
    {
        Context context = global::Android.App.Application.Context;
        string path = Path.Combine(context.CacheDir!.AbsolutePath, $"timer-{Guid.NewGuid():N}.wav");
        try
        {
            await File.WriteAllBytesAsync(path, TimerSoundAudio.CreateWave(sound)).ConfigureAwait(false);

            using MediaPlayer player = new MediaPlayer();
            try
            {
                TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
                player.Completion += (_, _) => completed.TrySetResult();

                player.Error += (_, args) =>
                {
                    args.Handled = true;
                    completed.TrySetException(new InvalidOperationException("Android audio playback failed."));
                };

                player.SetDataSource(path);
                player.Prepare();
                player.Start();

                await completed.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            finally
            {
                player.Release();
            }
        }
        finally
        {
            File.Delete(path);
        }
    }
}
