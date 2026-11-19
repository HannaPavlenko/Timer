using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timer.Controls;
using Timer.Core;
using Timer.ViewModels;
using Timer.Views;

namespace Timer.App.Tests;

/// <summary>Checks timer history across desktop window and Android activity lifetimes.</summary>
[TestClass]
public sealed class ViewLifetimeTests
{
    /// <summary>Creates the isolated UI environment used by the headless test session.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

    /// <summary>Elapsed time includes the gap between views and stays on the original history entry.</summary>
    [TestMethod]
    public async Task RecreatedActivityContinuesRunningTimerWithSameHistoryId()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(ViewLifetimeTests));
        await session.Dispatch(CheckRecreatedView, CancellationToken.None);
    }

    /// <summary>Closing the desktop window saves the elapsed time since the last refresh to the same record.</summary>
    [TestMethod]
    public async Task ClosingWindowPreservesElapsedTimeAndHistoryIdOnReopen()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(ViewLifetimeTests));
        await session.Dispatch(CheckClosedWindow, CancellationToken.None);
    }

    private static void CheckClosedWindow()
    {
        using TempHistory history = new();
        ManualClock clock = new();
        MainViewModel initial = new(history.Store, timeProvider: clock);
        MainWindow window = new() {DataContext = initial};
        window.Show();

        Guid id;
        try
        {
            initial.StartOrPauseCommand.Execute(null);
            id = initial.HistoryItems.Single().Id;
            clock.Seconds = 37;
        }
        finally
        {
            window.Close();
        }

        TimerHistoryStore reopenedStore = new(history.FilePath);
        MainViewModel reopened = new(reopenedStore);
        Assert.AreEqual(id, reopened.HistoryItems.Single().Id);

        TimerHistoryEntry entry = reopenedStore.Load().Single();
        Assert.AreEqual(id, entry.Id);
        Assert.AreEqual(TimeSpan.FromSeconds(37), entry.Elapsed);
        Assert.AreEqual(TimerHistoryStatus.Stopped, entry.Status);
    }

    /// <summary>Focusing cleared target fields preserves a finished count until a new goal is entered.</summary>
    [TestMethod]
    public async Task FinishedCountUpSurvivesFocusAndStartsWithNewTarget()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(ViewLifetimeTests));
        await session.Dispatch(CheckFinishedCountUp, CancellationToken.None);
    }

    private static void CheckFinishedCountUp()
    {
        using TempHistory history = new();
        ManualClock clock = new();
        MainViewModel model = new(history.Store, timeProvider: clock);
        MainWindow window = new() {DataContext = model};
        window.Show();

        try
        {
            model.SelectCountUpCommand.Execute(null);
            model.DurationMinutes = "0";
            model.DurationSeconds = "6";
            model.StartOrPauseCommand.Execute(null);
            clock.Seconds = 6;
            model.ResumeUpdates();

            TimerHistoryEntry completed = history.Store.Load().Single();
            Assert.AreEqual(TimerHistoryStatus.Completed, completed.Status);
            var fields = window.GetVisualDescendants().OfType<DigitsOnlyTextBox>()
                .ToDictionary(field => AutomationProperties.GetName(field)!);

            Assert.IsTrue(fields["Target time, minutes"].Focus());
            Assert.AreEqual(string.Empty, model.DurationMinutes);
            Assert.AreEqual("FINISHED", model.StatusLabel);
            Assert.AreEqual("00:06", model.DisplayTime);
            Assert.IsTrue(fields["Target time, seconds"].Focus());
            Assert.AreEqual("FINISHED", model.StatusLabel);
            Assert.AreEqual("00:06", model.DisplayTime);

            window.KeyTextInput("9");
            model.StartOrPauseCommand.Execute(null);
            Assert.AreEqual("RUNNING", model.StatusLabel);
            Assert.AreEqual("00:00", model.DisplayTime);

            var entries = history.Store.Load();
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(completed, entries.Single(entry => entry.Id == completed.Id));
            TimerHistoryEntry running = entries.Single(entry => entry.Id != completed.Id);
            Assert.AreEqual(TimerHistoryStatus.Running, running.Status);
            Assert.AreEqual(TimeSpan.FromSeconds(9), running.Target);
        }
        finally
        {
            window.Close();
        }
    }

    private static void CheckRecreatedView()
    {
        using TempHistory history = new();
        ManualClock clock = new();
        Func<MainView> createView = MainView.CreateActivityViewFactory(new MainViewModel(history.Store,
            timeProvider: clock));

        MainView first = createView();
        Window window = new() {Content = first};
        window.Show();

        try
        {
            MainViewModel initial = (MainViewModel)first.DataContext!;
            initial.StartOrPauseCommand.Execute(null);
            Guid id = history.Store.Load().Single().Id;
            clock.Seconds = 3;
            initial.ResumeUpdates();
            Assert.AreEqual("00:03", initial.DisplayTime);
            window.Content = null;
            clock.Seconds = 8;

            MainView recreated = createView();
            Assert.AreNotSame(first, recreated);
            window.Content = recreated;

            MainViewModel current = (MainViewModel)recreated.DataContext!;
            Assert.AreEqual("00:08", current.DisplayTime);
            Assert.IsTrue(current.IsTimerRunning);
            Assert.AreEqual(id, current.HistoryItems.Single().Id);

            TimerHistoryEntry running = history.Store.Load().Single();
            Assert.AreEqual(id, running.Id);
            Assert.AreEqual(TimerHistoryStatus.Running, running.Status);
            clock.Seconds = 11;
            current.StartOrPauseCommand.Execute(null);

            TimerHistoryEntry paused = history.Store.Load().Single();
            Assert.AreEqual(id, paused.Id);
            Assert.AreEqual(TimerHistoryStatus.Paused, paused.Status);
            Assert.AreEqual(TimeSpan.FromSeconds(11), paused.Elapsed);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        public long Seconds { get; set; }
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => Seconds;
    }

    private sealed class TempHistory : IDisposable
    {
        private readonly string _directory = Directory.CreateTempSubdirectory("Stilltime-").FullName;

        public TempHistory() => Store = new TimerHistoryStore(FilePath);

        public string FilePath => Path.Combine(_directory, "history.json");
        public TimerHistoryStore Store { get; }

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
