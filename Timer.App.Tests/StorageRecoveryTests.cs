using System.Text;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timer.Core;
using Timer.ViewModels;

namespace Timer.App.Tests;

[TestClass]
public sealed class StorageRecoveryTests
{
    private const string LegacyHistory = """
        [{
          "Id": "11223344-5566-7788-99aa-bbccddeeff00",
          "Mode": 0,
          "StartedAtUtc": "2026-06-10T10:00:00Z",
          "Status": 3,
          "Elapsed": "00:00:37"
        }]
        """;

    [TestMethod]
    public void FirstLaunchWithoutStorageDirectoryUsesDefaultsWithoutWarning()
    {
        using StorageDirectory directory = new();
        string path = directory.FilePath("Stilltime");
        TimerHistoryStore history = new(Path.Combine(path, "history.json"));
        SoundSettingsStore sound = new(Path.Combine(path, "sound.json"));
        MainViewModel viewModel = new(history, sound);

        Assert.IsFalse(viewModel.HasStorageError);
        Assert.IsTrue(viewModel.HasNoHistory);
        Assert.IsFalse(viewModel.IsSoundEnabled);

        viewModel.IsSoundEnabled = true;
        viewModel.StartOrPauseCommand.Execute(null);
        viewModel.ResetCommand.Execute(null);

        Assert.IsTrue(sound.Load());
        Assert.AreEqual(1, history.Load().Count);
    }

    [TestMethod]
    public void FileInPlaceOfStorageDirectoryShowsBothWarningsAtStartup()
    {
        using StorageDirectory directory = new();
        string path = directory.FilePath("Stilltime");
        File.WriteAllText(path, "existing file");

        MainViewModel viewModel = new(new TimerHistoryStore(Path.Combine(path, "history.json")),
            new SoundSettingsStore(Path.Combine(path, "sound.json")));

        Assert.IsTrue(viewModel.HasStorageError);
        StringAssert.Contains(viewModel.StorageErrorMessage, "history.json");
        StringAssert.Contains(viewModel.StorageErrorMessage, "sound.json");
        Assert.AreEqual("existing file", File.ReadAllText(path));
    }

    [TestMethod]
    [DataRow("Id")]
    [DataRow("Mode")]
    [DataRow("StartedAtUtc")]
    [DataRow("Status")]
    [DataRow("Elapsed")]
    public void MissingRequiredFieldShowsWarningAndPreservesOriginalBytes(string missingField)
    {
        using StorageDirectory directory = new();
        string path = directory.FilePath("history.json");
        JsonArray history = JsonNode.Parse(LegacyHistory)!.AsArray();
        Assert.IsTrue(history[0]!.AsObject().Remove(missingField));
        File.WriteAllText(path, history.ToJsonString(), Encoding.UTF8);
        byte[] original = File.ReadAllBytes(path);

        MainViewModel viewModel = new(new TimerHistoryStore(path));
        Assert.IsTrue(viewModel.HasStorageError);
        StringAssert.Contains(viewModel.StorageErrorMessage, "history.json");
        Assert.IsTrue(viewModel.HasNoHistory);
        viewModel.StartOrPauseCommand.Execute(null);
        viewModel.ResetCommand.Execute(null);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
    }

    [TestMethod]
    public void LegacyHistoryWithoutNameOrSettingsOpensWithoutWarning()
    {
        using StorageDirectory directory = new();
        string path = directory.FilePath("history.json");
        File.WriteAllText(path, LegacyHistory);

        MainViewModel viewModel = new(new TimerHistoryStore(path));
        Assert.IsFalse(viewModel.HasStorageError);
        TimerHistoryItemViewModel item = viewModel.HistoryItems.Single();
        Assert.AreEqual(Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00"), item.Id);
        Assert.AreEqual("00:37", item.DurationLabel);
        Assert.AreEqual(string.Empty, item.Name);
    }

    [TestMethod]
    [DataRow("{invalid json")]
    [DataRow("null")]
    [DataRow("[null]")]
    [DataRow("[{}]")]
    public void CorruptHistoryAllowsTimersWithoutOverwritingFile(string json)
    {
        using StorageDirectory directory = new StorageDirectory();
        string path = directory.FilePath("history.json");
        File.WriteAllText(path, json);
        MainViewModel viewModel = new MainViewModel(new TimerHistoryStore(path));

        Assert.IsTrue(viewModel.HasStorageError);
        StringAssert.Contains(viewModel.StorageErrorMessage, "history.json");
        Assert.IsTrue(viewModel.HasNoHistory);

        viewModel.StartOrPauseCommand.Execute(null);
        Guid id = viewModel.HistoryItems[0].Id;
        viewModel.StartOrPauseCommand.Execute(null);
        viewModel.HistoryItems[0].Name = "Temporary timer";
        viewModel.HistoryItems[0].RestartCommand.Execute(null);
        viewModel.HistoryItems[0].StopCommand.Execute(null);

        Assert.AreEqual("READY", viewModel.StatusLabel);
        Assert.AreEqual(id, viewModel.HistoryItems[0].Id);
        Assert.AreEqual("Temporary timer", viewModel.HistoryItems[0].Name);
        Assert.AreEqual(json, File.ReadAllText(path));

        viewModel.HistoryItems[0].DeleteCommand.Execute(null);

        Assert.IsTrue(viewModel.HasNoHistory);
        Assert.AreEqual(json, File.ReadAllText(path));
    }

    [TestMethod]
    public void CorruptSoundSettingsStayUnchangedWhileSoundAndHistoryWork()
    {
        using StorageDirectory directory = new StorageDirectory();
        string historyPath = directory.FilePath("history.json");
        string soundPath = directory.FilePath("sound.json");
        File.WriteAllText(soundPath, "{invalid json");
        MainViewModel viewModel = new MainViewModel(new TimerHistoryStore(historyPath),
            new SoundSettingsStore(soundPath));

        Assert.IsFalse(viewModel.IsSoundEnabled);
        Assert.IsTrue(viewModel.HasStorageError);
        StringAssert.Contains(viewModel.StorageErrorMessage, "sound.json");

        viewModel.IsSoundEnabled = true;
        viewModel.StartOrPauseCommand.Execute(null);
        viewModel.ResetCommand.Execute(null);

        Assert.IsTrue(viewModel.IsSoundEnabled);
        Assert.AreEqual("{invalid json", File.ReadAllText(soundPath));
        Assert.AreEqual(1, new TimerHistoryStore(historyPath).Load().Count);
    }

    [TestMethod]
    public void WriteFailurePreservesCurrentTimerAndStopsFurtherDiskAttempts()
    {
        FailingHistoryStore store = new FailingHistoryStore();
        MainViewModel viewModel = new MainViewModel(store);
        viewModel.StartOrPauseCommand.Execute(null);
        Guid id = viewModel.HistoryItems[0].Id;
        store.FailWrites = true;
        viewModel.StartOrPauseCommand.Execute(null);

        Assert.IsTrue(viewModel.HasStorageError);
        Assert.AreEqual("PAUSED", viewModel.StatusLabel);
        Assert.AreEqual(TimerHistoryStatus.Running, store.Entries[0].Status);

        store.FailReads = true;
        viewModel.HistoryItems[0].Name = "Local name";
        viewModel.HistoryItems[0].RestartCommand.Execute(null);
        viewModel.HistoryItems[0].StopCommand.Execute(null);

        Assert.AreEqual(id, viewModel.HistoryItems[0].Id);
        Assert.AreEqual("Local name", viewModel.HistoryItems[0].Name);
        Assert.IsTrue(viewModel.HistoryItems[0].CanRestart);
        Assert.AreEqual(1, store.Failures);

        viewModel.HistoryItems[0].DeleteCommand.Execute(null);
        viewModel.ResetCommand.Execute(null);

        Assert.IsTrue(viewModel.HasNoHistory);
        Assert.AreEqual(1, store.Failures);
    }

    [TestMethod]
    public void DeleteFailureDoesNotResurrectActiveEntry()
    {
        FailingHistoryStore store = new FailingHistoryStore();
        MainViewModel viewModel = new MainViewModel(store);
        viewModel.StartOrPauseCommand.Execute(null);
        store.FailDeletes = true;
        viewModel.HistoryItems[0].DeleteCommand.Execute(null);

        Assert.IsTrue(viewModel.HasStorageError);
        Assert.AreEqual("RUNNING", viewModel.StatusLabel);
        Assert.IsTrue(viewModel.HasNoHistory);

        viewModel.StartOrPauseCommand.Execute(null);
        viewModel.ResetCommand.Execute(null);

        Assert.IsTrue(viewModel.HasNoHistory);
        Assert.AreEqual(1, store.Entries.Count);
        Assert.AreEqual(1, store.Failures);
    }

    [TestMethod]
    public void ReadFailureUsesLastKnownHistoryAndPreservesFifoLimit()
    {
        FailingHistoryStore store = new FailingHistoryStore();
        MainViewModel viewModel = new MainViewModel(store);
        viewModel.StartOrPauseCommand.Execute(null);
        viewModel.ResetCommand.Execute(null);
        Guid firstId = viewModel.HistoryItems[0].Id;
        store.FailReads = true;
        viewModel.SelectCountdownCommand.Execute(null);
        viewModel.SelectStopwatchCommand.Execute(null);

        Assert.IsTrue(viewModel.HasStorageError);
        Assert.AreEqual(firstId, viewModel.HistoryItems[0].Id);

        for (int index = 0; index < 10; index++)
        {
            viewModel.StartOrPauseCommand.Execute(null);
            viewModel.ResetCommand.Execute(null);
        }

        Assert.AreEqual(10, viewModel.HistoryItems.Count);
        Assert.IsFalse(viewModel.HistoryItems.Any(item => item.Id == firstId));
        Assert.AreEqual(1, store.Entries.Count);
        Assert.AreEqual(1, store.Failures);
    }

    [TestMethod]
    public void StartupWriteFailureRecoversRunningHistoryInMemory()
    {
        FailingHistoryStore store = new FailingHistoryStore();
        TimerHistoryEntry entry = new TimerHistoryEntry(Guid.NewGuid(), TimerMode.Stopwatch,
            DateTimeOffset.UtcNow, TimerHistoryStatus.Running, TimeSpan.FromSeconds(7));
        store.Entries.Add(entry);
        store.FailWrites = true;
        MainViewModel viewModel = new MainViewModel(store);

        Assert.IsTrue(viewModel.HasStorageError);
        Assert.AreEqual("Stopped", viewModel.HistoryItems[0].StatusLabel);
        Assert.AreEqual("00:07", viewModel.HistoryItems[0].DurationLabel);
        Assert.AreEqual(TimerHistoryStatus.Running, store.Entries[0].Status);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SoundAccessFailuresAreIsolatedFromHistory(bool failOnLoad)
    {
        FailingHistoryStore history = new FailingHistoryStore();
        FailingSoundStore sound = new FailingSoundStore {FailOnLoad = failOnLoad};
        MainViewModel viewModel = new MainViewModel(history, sound);
        List<string?> notifications = [];
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        viewModel.IsSoundEnabled = true;
        viewModel.IsSoundEnabled = false;
        viewModel.IsSoundEnabled = true;
        viewModel.StartOrPauseCommand.Execute(null);
        viewModel.ResetCommand.Execute(null);

        Assert.IsTrue(viewModel.HasStorageError);
        Assert.IsTrue(viewModel.IsSoundEnabled);
        Assert.AreEqual(1, sound.Failures);
        Assert.AreEqual(1, history.Entries.Count);
        if (!failOnLoad)
        {
            CollectionAssert.Contains(notifications, nameof(MainViewModel.HasStorageError));
            CollectionAssert.Contains(notifications, nameof(MainViewModel.StorageErrorMessage));
        }
    }

    [TestMethod]
    public void RenamePersistsAcrossRestartStopAndReopening()
    {
        using StorageDirectory directory = new StorageDirectory();
        string path = directory.FilePath("history.json");
        MainViewModel viewModel = new MainViewModel(new TimerHistoryStore(path));
        viewModel.StartOrPauseCommand.Execute(null);
        Guid id = viewModel.HistoryItems[0].Id;
        DateTimeOffset startedAt = new TimerHistoryStore(path).Load()[0].StartedAtUtc;
        viewModel.HistoryItems[0].Name = "Named timer";

        Assert.AreEqual(startedAt, new TimerHistoryStore(path).Load()[0].StartedAtUtc);

        viewModel.HistoryItems[0].StopCommand.Execute(null);
        viewModel.HistoryItems[0].RestartCommand.Execute(null);
        viewModel.HistoryItems[0].StopCommand.Execute(null);
        MainViewModel reopened = new MainViewModel(new TimerHistoryStore(path));

        Assert.IsFalse(reopened.HasStorageError);
        Assert.AreEqual(1, reopened.HistoryItems.Count);
        Assert.AreEqual(id, reopened.HistoryItems[0].Id);
        Assert.AreEqual("Named timer", reopened.HistoryItems[0].Name);
    }

    [TestMethod]
    public void BothStorageFailuresRemainVisible()
    {
        MainViewModel viewModel = new MainViewModel(new FailingHistoryStore {FailReads = true},
            new FailingSoundStore {FailOnLoad = true});

        Assert.IsTrue(viewModel.HasStorageError);
        StringAssert.Contains(viewModel.StorageErrorMessage, "history.json and sound.json");
        viewModel.StartOrPauseCommand.Execute(null);
        viewModel.ResetCommand.Execute(null);
    }

    [TestMethod]
    public void HistoryFailureDoesNotPreventSavingSoundPreference()
    {
        using StorageDirectory directory = new StorageDirectory();
        string path = directory.FilePath("sound.json");
        MainViewModel viewModel = new MainViewModel(new FailingHistoryStore {FailReads = true},
            new SoundSettingsStore(path));
        viewModel.IsSoundEnabled = true;

        Assert.IsTrue(viewModel.HasStorageError);
        Assert.IsTrue(new SoundSettingsStore(path).Load());

        viewModel.IsSoundEnabled = false;

        Assert.IsFalse(new SoundSettingsStore(path).Load());
    }

    [TestMethod]
    public void CompletionWriteFailureStillFinishesAndAllowsRestartInMemory()
    {
        FailingHistoryStore store = new FailingHistoryStore();
        ManualTimeProvider clock = new ManualTimeProvider();
        MainViewModel viewModel = new MainViewModel(store, timeProvider: clock);
        viewModel.SelectCountdownCommand.Execute(null);
        viewModel.DurationSeconds = "2";
        viewModel.StartOrPauseCommand.Execute(null);
        Guid id = viewModel.HistoryItems[0].Id;
        store.FailWrites = true;
        clock.Advance(TimeSpan.FromSeconds(3));
        viewModel.ResumeUpdates();

        Assert.IsTrue(viewModel.HasStorageError);
        Assert.AreEqual("FINISHED", viewModel.StatusLabel);
        Assert.AreEqual("00:00", viewModel.DisplayTime);
        Assert.IsTrue(viewModel.HistoryItems[0].CanRestart);

        viewModel.HistoryItems[0].RestartCommand.Execute(null);

        Assert.AreEqual("RUNNING", viewModel.StatusLabel);
        Assert.AreEqual(1, viewModel.HistoryItems.Count);
        Assert.AreEqual(id, viewModel.HistoryItems[0].Id);
        Assert.AreEqual(1, store.Failures);
        viewModel.ResetCommand.Execute(null);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan elapsed) => _timestamp += elapsed.Ticks;
    }

    private sealed class FailingHistoryStore : ITimerHistoryStore
    {
        public List<TimerHistoryEntry> Entries { get; } = [];
        public bool FailDeletes { get; set; }
        public bool FailReads { get; set; }
        public bool FailWrites { get; set; }
        public int Failures { get; private set; }

        public IReadOnlyList<TimerHistoryEntry> Load()
        {
            if (FailReads)
            {
                Failures++;
                throw new IOException("Simulated read failure.");
            }

            return Entries.ToArray();
        }

        public void Upsert(TimerHistoryEntry entry)
        {
            if (FailWrites)
            {
                Failures++;
                throw new IOException("Simulated write failure.");
            }

            int index = Entries.FindIndex(existing => existing.Id == entry.Id);
            if (index < 0)
                Entries.Add(entry);
            else
                Entries[index] = entry;
        }

        public bool Delete(Guid id)
        {
            if (FailDeletes)
            {
                Failures++;
                throw new IOException("Simulated delete failure.");
            }

            return Entries.RemoveAll(entry => entry.Id == id) > 0;
        }
    }

    private sealed class FailingSoundStore : ISoundSettingsStore
    {
        public bool FailOnLoad { get; init; }
        public int Failures { get; private set; }

        public bool Load()
        {
            if (FailOnLoad)
            {
                Failures++;
                throw new UnauthorizedAccessException("Simulated settings access failure.");
            }

            return false;
        }

        public void Save(bool enabled)
        {
            Failures++;
            throw new UnauthorizedAccessException("Simulated settings access failure.");
        }
    }

    private sealed class StorageDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "StilltimeTests", Guid.NewGuid().ToString("N"));

        public StorageDirectory() => Directory.CreateDirectory(_path);

        public string FilePath(string name) => Path.Combine(_path, name);

        public void Dispose() => Directory.Delete(_path, recursive: true);
    }
}
