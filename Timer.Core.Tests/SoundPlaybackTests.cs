using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Timer.Core.Tests;

/// <summary>Verifies nonblocking playback, overlap suppression, and recovery from audio failures.</summary>
[TestClass]
public sealed class SoundPlaybackTests
{
    /// <summary>Checks that a pending tone does not block the caller or queue duplicate tones.</summary>
    [TestMethod]
    public async Task ConcurrentRequestsDoNotQueueExtraSounds()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        var player = new SoundPlayback(_ =>
        {
            calls++;
            return pending.Task;
        });

        var first = player.PlayAsync();
        Assert.IsFalse(first.IsCompleted);

        await player.PlayAsync();
        Assert.AreEqual(1, calls);
        pending.SetResult();
        await first;
        await player.PlayAsync();
        Assert.AreEqual(2, calls);
    }

    /// <summary>Checks both synchronous and asynchronous device failures without breaking future playback.</summary>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task AudioFailuresDoNotEscapeAndNextRequestCanPlay(bool synchronousFailure)
    {
        var calls = 0;

        var player = new SoundPlayback(_ =>
        {
            calls++;

            if (calls == 1)
            {
                if (synchronousFailure)
                    throw new IOException("Audio device unavailable.");

                return Task.FromException(new IOException("Audio device unavailable."));
            }

            return Task.CompletedTask;
        });

        await player.PlayAsync();
        await player.PlayAsync();
        Assert.AreEqual(2, calls);
    }
}
