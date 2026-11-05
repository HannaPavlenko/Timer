using Microsoft.VisualStudio.TestTools.UnitTesting;
using Avalonia.Controls;
using Timer.Controls;
using Timer.ViewModels;
using Timer.Core;

namespace Timer.App.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public void NumericTimeInputKeepsOnlyAsciiDigits()
    {
        var input = new DigitsOnlyTextBox { Text = "вап12a3" };

        Assert.AreEqual(typeof(TextBox), input.StyleKey);
        Assert.AreEqual("123", input.Text);
        input.Text = "4 5-6";
        Assert.AreEqual("456", input.Text);
    }

    [TestMethod]
    public void CountdownShowsPresetAndRejectsInvalidSeconds()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectCountdownCommand.Execute(null);
        viewModel.DurationMinutes = "5";

        Assert.AreEqual("05:00", viewModel.DisplayTime);
        Assert.IsTrue(viewModel.IsDurationMode);

        viewModel.DurationSeconds = "75";
        Assert.IsTrue(viewModel.HasError);
        Assert.AreEqual("00:00", viewModel.DisplayTime);

        viewModel.DurationSeconds = "30";
        Assert.IsFalse(viewModel.HasError);
        Assert.AreEqual("05:30", viewModel.DisplayTime);
    }

    [TestMethod]
    public void StartPauseResumeAndResetUpdateControls()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectCountUpCommand.Execute(null);
        viewModel.DurationMinutes = "0";
        viewModel.DurationSeconds = "5";

        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual("RUNNING", viewModel.StatusLabel);
        Assert.AreEqual("Pause timer", viewModel.PrimaryActionLabel);
        Assert.IsFalse(viewModel.CanEditSettings);

        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual("PAUSED", viewModel.StatusLabel);
        Assert.AreEqual("Resume timer", viewModel.PrimaryActionLabel);

        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual("RUNNING", viewModel.StatusLabel);

        viewModel.ResetCommand.Execute(null);
        Assert.AreEqual("READY", viewModel.StatusLabel);
        Assert.AreEqual("00:00", viewModel.DisplayTime);
        Assert.IsTrue(viewModel.CanEditSettings);
    }

    [TestMethod]
    public void SwitchingModesResetsTheSession()
    {
        var viewModel = CreateViewModel();
        Assert.IsFalse(viewModel.HasDetailLabel);
        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual("RUNNING", viewModel.StatusLabel);

        viewModel.WorkSeconds = "45";
        viewModel.RestSeconds = "15";
        viewModel.Rounds = "4";
        viewModel.SelectedModeIndex = 3;
        Assert.IsTrue(viewModel.IsIntervalsSelected);
        Assert.AreEqual(3, viewModel.SelectedModeIndex);
        Assert.AreEqual("READY", viewModel.StatusLabel);
        Assert.AreEqual("0", viewModel.PreparationSeconds);
        Assert.AreEqual("00:45", viewModel.DisplayTime);
        Assert.AreEqual("WORK INTERVAL", viewModel.PhaseLabel);
        Assert.AreEqual("Round 1 of 4  ·  work", viewModel.DetailLabel);
        Assert.IsTrue(viewModel.CanEditSettings);

        viewModel.SelectCountdownCommand.Execute(null);
        Assert.AreEqual(2, viewModel.SelectedModeIndex);
        Assert.IsTrue(viewModel.IsDurationMode);
    }

    [TestMethod]
    public void CountUpAndCountdownKeepSeparateTargets()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectCountUpCommand.Execute(null);
        viewModel.DurationMinutes = "1";
        viewModel.DurationSeconds = "20";

        viewModel.SelectCountdownCommand.Execute(null);
        viewModel.DurationMinutes = "2";
        viewModel.DurationSeconds = "30";
        Assert.AreEqual("02:30", viewModel.DisplayTime);

        viewModel.SelectCountUpCommand.Execute(null);
        Assert.AreEqual("1", viewModel.DurationMinutes);
        Assert.AreEqual("20", viewModel.DurationSeconds);
        Assert.AreEqual("Goal  01:20", viewModel.DetailLabel);

        viewModel.SelectCountdownCommand.Execute(null);
        Assert.AreEqual("2", viewModel.DurationMinutes);
        Assert.AreEqual("30", viewModel.DurationSeconds);
        Assert.AreEqual("02:30", viewModel.DisplayTime);
    }

    [TestMethod]
    public void SixSecondsStartsWithEmptyMinutesInBothDurationModes()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectCountUpCommand.Execute(null);
        viewModel.DurationMinutes = "";
        viewModel.DurationSeconds = "6";

        Assert.IsFalse(viewModel.HasError);
        Assert.AreEqual("Goal  00:06", viewModel.DetailLabel);
        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual("RUNNING", viewModel.StatusLabel);

        viewModel.SelectCountdownCommand.Execute(null);
        viewModel.DurationMinutes = "";
        viewModel.DurationSeconds = "6";

        Assert.IsFalse(viewModel.HasError);
        Assert.AreEqual("00:06", viewModel.DisplayTime);
        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual("RUNNING", viewModel.StatusLabel);
    }

    [TestMethod]
    public void HistoryPersistsAcrossViewModelsAndDeleteRemovesAnActiveEntry()
    {
        var store = new MemoryHistoryStore();
        var viewModel = new MainViewModel(store);
        Assert.IsTrue(viewModel.HasNoHistory);

        viewModel.SelectCountUpCommand.Execute(null);
        viewModel.DurationSeconds = "6";
        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual(1, viewModel.HistoryItems.Count);
        Assert.AreEqual("Count up", viewModel.HistoryItems[0].ModeLabel);
        Assert.AreEqual("Target 00:06", viewModel.HistoryItems[0].SettingsLabel);

        var reopened = new MainViewModel(store);
        Assert.IsTrue(reopened.HasNoHistory);
        reopened.SelectCountUpCommand.Execute(null);
        Assert.AreEqual("Stopped", reopened.HistoryItems[0].StatusLabel);
        reopened.HistoryItems[0].DeleteCommand.Execute(null);
        Assert.IsTrue(reopened.HasNoHistory);
        Assert.AreEqual(0, store.Load().Count);

        viewModel.HistoryItems[0].DeleteCommand.Execute(null);
        viewModel.ResetCommand.Execute(null);
        Assert.AreEqual(0, store.Load().Count);
    }

    [TestMethod]
    public void HistoryShowsOnlyTheSelectedModeAndKeepsOtherModesAfterDeletion()
    {
        var store = new MemoryHistoryStore();
        foreach (var mode in Enum.GetValues<TimerMode>())
            store.Upsert(new TimerHistoryEntry(
                Guid.NewGuid(), mode, DateTimeOffset.UtcNow,
                TimerHistoryStatus.Stopped, TimeSpan.FromSeconds(2)));

        var viewModel = new MainViewModel(store);
        Assert.AreEqual(1, viewModel.HistoryItems.Count);
        Assert.AreEqual("Stopwatch", viewModel.HistoryItems[0].ModeLabel);

        viewModel.SelectCountUpCommand.Execute(null);
        Assert.AreEqual("Count up", viewModel.HistoryItems[0].ModeLabel);
        viewModel.HistoryItems[0].DeleteCommand.Execute(null);
        Assert.IsTrue(viewModel.HasNoHistory);

        viewModel.SelectCountdownCommand.Execute(null);
        Assert.AreEqual("Countdown", viewModel.HistoryItems[0].ModeLabel);
        viewModel.SelectIntervalsCommand.Execute(null);
        Assert.AreEqual("Intervals", viewModel.HistoryItems[0].ModeLabel);
        viewModel.SelectStopwatchCommand.Execute(null);
        Assert.AreEqual("Stopwatch", viewModel.HistoryItems[0].ModeLabel);
        Assert.AreEqual(3, store.Load().Count);
    }

    private static MainViewModel CreateViewModel() => new(new MemoryHistoryStore());

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
