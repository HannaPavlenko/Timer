using System.Diagnostics;

namespace Timer.Core;

/// <summary>Plays one platform sound at a time without interrupting timer updates on audio failures.</summary>
public sealed class SoundPlayback
{
    private readonly Func<TimerSound, Task> _playSoundAsync;
    private int _isPlaying;

    /// <summary>Wraps a platform implementation that completes when its short sound ends.</summary>
    public SoundPlayback(Func<TimerSound, Task> playSoundAsync)
    {
        ArgumentNullException.ThrowIfNull(playSoundAsync);
        _playSoundAsync = playSoundAsync;
    }

    /// <summary>Plays a sound, skipping overlapping requests and recording audio failures.</summary>
    public async Task PlayAsync(TimerSound sound = TimerSound.Completion)
    {
        if (Interlocked.CompareExchange(ref _isPlaying, 1, 0) != 0)
            return;

        try
        {
            await _playSoundAsync(sound).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"Timer sound could not play: {exception.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isPlaying, 0);
        }
    }
}
