using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timer.Core;

namespace Timer.Core.Tests;

[TestClass]
public sealed class TimerEngineTests
{
    [TestMethod]
    public void StopwatchPausesAndResumesWithoutCountingPausedTime()
    {
        var clock = new ManualTimeProvider();
        var timer = new TimerEngine(clock);

        timer.Start();
        clock.Advance(TimeSpan.FromSeconds(12));
        timer.Pause();
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.AreEqual(TimeSpan.FromSeconds(12), timer.Update().Elapsed);

        timer.Start();
        clock.Advance(TimeSpan.FromSeconds(3));
        var snapshot = timer.Update();
        Assert.AreEqual(TimerStatus.Running, snapshot.Status);
        Assert.AreEqual(TimeSpan.FromSeconds(15), snapshot.Elapsed);
    }

    [TestMethod]
    public void CountUpStopsAtTargetEvenWhenRefreshIsLate()
    {
        var clock = new ManualTimeProvider();
        var timer = new TimerEngine(clock);
        timer.ConfigureCountUp(TimeSpan.FromSeconds(10));

        timer.Start();
        clock.Advance(TimeSpan.FromSeconds(14));
        var snapshot = timer.Update();

        Assert.AreEqual(TimerStatus.Completed, snapshot.Status);
        Assert.AreEqual(TimeSpan.FromSeconds(10), snapshot.Elapsed);
        Assert.AreEqual(TimeSpan.Zero, snapshot.Remaining);
        Assert.AreEqual(1d, snapshot.Progress);
    }

    [TestMethod]
    public void CountdownCanResetAndRestartAfterCompletion()
    {
        var clock = new ManualTimeProvider();
        var timer = new TimerEngine(clock);
        timer.ConfigureCountdown(TimeSpan.FromSeconds(20));

        timer.Start();
        clock.Advance(TimeSpan.FromSeconds(7));
        Assert.AreEqual(TimeSpan.FromSeconds(13), timer.Update().Remaining);
        timer.Reset();
        Assert.AreEqual(TimeSpan.FromSeconds(20), timer.Update().Remaining);

        timer.Start();
        clock.Advance(TimeSpan.FromSeconds(20));
        Assert.AreEqual(TimerStatus.Completed, timer.Update().Status);
        timer.Start();
        Assert.AreEqual(TimeSpan.FromSeconds(20), timer.Update().Remaining);
    }

    [TestMethod]
    public void IntervalsAdvanceAcrossSkippedRefreshes()
    {
        var clock = new ManualTimeProvider();
        var timer = new TimerEngine(clock);
        timer.ConfigureIntervals(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), 3);
        timer.Start();

        clock.Advance(TimeSpan.FromSeconds(10));
        var rest = timer.Update();
        Assert.AreEqual(IntervalPhase.Rest, rest.Phase);
        Assert.AreEqual(1, rest.Round);
        Assert.AreEqual(TimeSpan.FromSeconds(5), rest.Remaining);

        clock.Advance(TimeSpan.FromSeconds(18));
        var secondRest = timer.Update();
        Assert.AreEqual(IntervalPhase.Rest, secondRest.Phase);
        Assert.AreEqual(2, secondRest.Round);
        Assert.AreEqual(TimeSpan.FromSeconds(2), secondRest.Remaining);
    }

    [TestMethod]
    public void IntervalsFinishAfterFinalWorkWithoutFinalRest()
    {
        var clock = new ManualTimeProvider();
        var timer = new TimerEngine(clock);
        timer.ConfigureIntervals(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), 2);
        timer.Start();

        clock.Advance(TimeSpan.FromSeconds(25));
        var snapshot = timer.Update();

        Assert.AreEqual(TimerStatus.Completed, snapshot.Status);
        Assert.AreEqual(IntervalPhase.Complete, snapshot.Phase);
        Assert.AreEqual(2, snapshot.Round);
        Assert.AreEqual(TimeSpan.FromSeconds(25), snapshot.TotalDuration);
        Assert.AreEqual(TimeSpan.Zero, snapshot.Remaining);
    }

    [TestMethod]
    public void SingleRoundHasNoRestPhase()
    {
        var clock = new ManualTimeProvider();
        var timer = new TimerEngine(clock);
        timer.ConfigureIntervals(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), 1);
        timer.Start();

        clock.Advance(TimeSpan.FromSeconds(10));
        var snapshot = timer.Update();

        Assert.AreEqual(TimerStatus.Completed, snapshot.Status);
        Assert.AreEqual(TimeSpan.FromSeconds(10), snapshot.TotalDuration);
    }

    [TestMethod]
    public void IntervalsPauseWithinPhase()
    {
        var clock = new ManualTimeProvider();
        var timer = new TimerEngine(clock);
        timer.ConfigureIntervals(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), 2);
        timer.Start();
        clock.Advance(TimeSpan.FromSeconds(12));
        timer.Pause();
        clock.Advance(TimeSpan.FromSeconds(20));

        var snapshot = timer.Update();
        Assert.AreEqual(TimerStatus.Paused, snapshot.Status);
        Assert.AreEqual(IntervalPhase.Rest, snapshot.Phase);
        Assert.AreEqual(TimeSpan.FromSeconds(3), snapshot.Remaining);
    }

    [TestMethod]
    public void InvalidSettingsDoNotReplaceCurrentSession()
    {
        var timer = new TimerEngine(new ManualTimeProvider());
        timer.ConfigureCountdown(TimeSpan.FromSeconds(30));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => timer.ConfigureCountUp(TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => timer.ConfigureIntervals(
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => timer.ConfigureIntervals(
            TimeSpan.MaxValue, TimeSpan.MaxValue, 2));
        Assert.AreEqual(TimerMode.Countdown, timer.Update().Mode);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan duration) => _timestamp += duration.Ticks;
    }
}
