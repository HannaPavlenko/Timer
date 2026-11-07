using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timer.Core;
using Timer.ViewModels;

namespace Timer.App.Tests;

/// <summary>Verifies persisted sound preferences and countdown boundary notifications without playing audio.</summary>
[TestClass]
public sealed class SoundTests
{
    /// <summary>Checks that the shared preference is off initially and survives reopening.</summary>
    [TestMethod]
    public void SoundPreferencePersistsAcrossModesAndInstances()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"stilltime-sound-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SoundSettingsStore(filePath);
            Assert.IsFalse(store.Load());

            var viewModel = new MainViewModel(new MemoryHistoryStore(), store);
            Assert.IsFalse(viewModel.IsSoundEnabled);
            viewModel.IsSoundEnabled = true;
            viewModel.SelectIntervalsCommand.Execute(null);
            Assert.IsTrue(viewModel.IsSoundEnabled);

            var reopened = new MainViewModel(new MemoryHistoryStore(), new SoundSettingsStore(filePath));
            Assert.IsTrue(reopened.IsSoundEnabled);
            reopened.IsSoundEnabled = false;
            Assert.IsFalse(new SoundSettingsStore(filePath).Load());
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>Checks completion, duplicate refreshes, mute, and restart for timed modes.</summary>
    [TestMethod]
    [DataRow(TimerMode.CountUp, true)]
    [DataRow(TimerMode.Countdown, true)]
    [DataRow(TimerMode.CountUp, false)]
    [DataRow(TimerMode.Countdown, false)]
    public void TimedModesRequestOneSoundPerCompletion(TimerMode mode, bool soundEnabled)
    {
        var clock = new ManualClock();
        var viewModel = new MainViewModel(new MemoryHistoryStore(), timeProvider: clock);
        viewModel.SelectedModeIndex = (int)mode;
        viewModel.DurationSeconds = "2";
        viewModel.IsSoundEnabled = soundEnabled;

        var signals = 0;
        viewModel.SoundRequested += (_, _) => signals++;
        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual(0, signals);

        clock.Advance(3);
        viewModel.ResumeUpdates();
        viewModel.ResumeUpdates();
        Assert.AreEqual(soundEnabled ? 1 : 0, signals);

        viewModel.ResetCommand.Execute(null);
        viewModel.HistoryItems[0].RestartCommand.Execute(null);
        Assert.AreEqual(soundEnabled ? 1 : 0, signals);
        clock.Advance(3);
        viewModel.ResumeUpdates();
        Assert.AreEqual(soundEnabled ? 2 : 0, signals);
    }

    /// <summary>Checks preparation, every work/rest boundary, and the final rest without duplicate signals.</summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(3)]
    public void IntervalsRequestSoundAtEveryObservedBoundary(int preparationSeconds)
    {
        var clock = new ManualClock();
        var viewModel = new MainViewModel(new MemoryHistoryStore(), timeProvider: clock);
        viewModel.SelectIntervalsCommand.Execute(null);
        viewModel.PreparationSeconds = preparationSeconds.ToString();
        viewModel.WorkSeconds = "4";
        viewModel.RestSeconds = "5";
        viewModel.Rounds = "2";
        viewModel.IsSoundEnabled = true;

        var signals = 0;
        var cues = new List<TimerSound>();
        viewModel.SoundRequested += (_, args) => cues.Add(args.Sound);
        viewModel.SoundRequested += (_, _) => signals++;
        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual(1, signals);

        var expected = 1;
        viewModel.StartOrPauseCommand.Execute(null);
        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual(1, signals);

        if (preparationSeconds > 0)
        {
            clock.Advance(preparationSeconds);
            viewModel.ResumeUpdates();
            Assert.AreEqual(++expected, signals);
        }

        foreach (var duration in new[] {4, 5, 4, 5})
        {
            clock.Advance(duration);
            viewModel.ResumeUpdates();
            Assert.AreEqual(++expected, signals);
            viewModel.ResumeUpdates();
            Assert.AreEqual(expected, signals);
        }

        Assert.AreEqual("FINISHED", viewModel.StatusLabel);
        var expectedCues = preparationSeconds > 0
            ? new[] {TimerSound.Prepare, TimerSound.Work, TimerSound.Rest, TimerSound.Work, TimerSound.Rest, TimerSound.Gong}
            : new[] {TimerSound.Work, TimerSound.Rest, TimerSound.Work, TimerSound.Rest, TimerSound.Gong};
        CollectionAssert.AreEqual(expectedCues, cues);
        viewModel.HistoryItems[0].RestartCommand.Execute(null);
        Assert.AreEqual(preparationSeconds > 0 ? TimerSound.Prepare : TimerSound.Work, cues[^1]);
    }

    /// <summary>Checks that manual controls and stopwatch activity do not emit completion signals.</summary>
    [TestMethod]
    public void PauseResetAndModeSwitchAreSilent()
    {
        var clock = new ManualClock();
        var viewModel = new MainViewModel(new MemoryHistoryStore(), timeProvider: clock);
        viewModel.SelectCountUpCommand.Execute(null);
        viewModel.DurationSeconds = "10";
        viewModel.IsSoundEnabled = true;

        var signals = 0;
        viewModel.SoundRequested += (_, _) => signals++;
        viewModel.StartOrPauseCommand.Execute(null);
        clock.Advance(1);
        viewModel.StartOrPauseCommand.Execute(null);
        clock.Advance(30);
        viewModel.ResumeUpdates();
        viewModel.StartOrPauseCommand.Execute(null);
        viewModel.ResetCommand.Execute(null);
        viewModel.SelectStopwatchCommand.Execute(null);
        viewModel.StartOrPauseCommand.Execute(null);
        clock.Advance(100);
        viewModel.ResumeUpdates();
        viewModel.ResetCommand.Execute(null);
        Assert.AreEqual(0, signals);
    }

    /// <summary>Checks toggling sound mid-run without replaying muted interval boundaries.</summary>
    [TestMethod]
    public void EnablingSoundDoesNotReplayMutedBoundaries()
    {
        var clock = new ManualClock();
        var viewModel = new MainViewModel(new MemoryHistoryStore(), timeProvider: clock);
        viewModel.SelectIntervalsCommand.Execute(null);
        viewModel.WorkSeconds = "2";
        viewModel.RestSeconds = "2";

        var signals = 0;
        viewModel.SoundRequested += (_, _) => signals++;
        viewModel.StartOrPauseCommand.Execute(null);
        clock.Advance(2);
        viewModel.IsSoundEnabled = true;
        viewModel.ResumeUpdates();
        Assert.AreEqual(0, signals);
        clock.Advance(2);
        viewModel.ResumeUpdates();
        Assert.AreEqual(1, signals);
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(int seconds) => _timestamp += TimeSpan.FromSeconds(seconds).Ticks;
    }

    private sealed class MemoryHistoryStore : ITimerHistoryStore
    {
        private readonly List<TimerHistoryEntry> _entries = [];

        public IReadOnlyList<TimerHistoryEntry> Load() => _entries.ToArray();

        public void Upsert(TimerHistoryEntry entry)
        {
            var index = _entries.FindIndex(existing => existing.Id == entry.Id);
            if (index < 0)
                _entries.Add(entry);
            else
                _entries[index] = entry;
        }

        public bool Delete(Guid id) => _entries.RemoveAll(entry => entry.Id == id) > 0;
    }
}
