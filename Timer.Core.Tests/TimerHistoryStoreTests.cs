using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timer.Core;
using System.Text.Json;

namespace Timer.Core.Tests;

[TestClass]
public sealed class TimerHistoryStoreTests
{
    [TestMethod]
    public void SavesUpdatesAndDeletesEntriesAcrossStoreInstances()
    {
        string directory = Path.Combine(Path.GetTempPath(), "StilltimeTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "history.json");

        try
        {
            TimerHistoryStore store = new TimerHistoryStore(path);
            Assert.AreEqual(0, store.Load().Count);

            TimerHistoryEntry first = NewEntry(TimerMode.CountUp) with { Target = TimeSpan.FromSeconds(6) };
            TimerHistoryEntry second = NewEntry(TimerMode.Intervals) with
            {
                Preparation = TimeSpan.Zero,
                Work = TimeSpan.FromSeconds(45),
                Rest = TimeSpan.FromSeconds(15),
                Rounds = 3
            };

            store.Upsert(first);
            store.Upsert(second);

            TimerHistoryStore reopened = new TimerHistoryStore(path);
            CollectionAssert.AreEqual(new[] { first, second }, reopened.Load().ToArray());

            TimerHistoryEntry paused = first with
            {
                Status = TimerHistoryStatus.Paused,
                Elapsed = TimeSpan.FromSeconds(3)
            };
            reopened.Upsert(paused);
            CollectionAssert.AreEqual(new[] { paused, second }, store.Load().ToArray());

            Assert.IsTrue(store.Delete(first.Id));
            Assert.IsFalse(store.Delete(first.Id));
            CollectionAssert.AreEqual(new[] { second }, reopened.Load().ToArray());
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void InvalidJsonDoesNotGetOverwritten()
    {
        string directory = Path.Combine(Path.GetTempPath(), "StilltimeTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "history.json");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{invalid json");
            TimerHistoryStore store = new TimerHistoryStore(path);

            Assert.ThrowsExactly<InvalidDataException>(() => store.Upsert(NewEntry(TimerMode.Stopwatch)));
            Assert.AreEqual("{invalid json", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void EleventhEntryEvictsFirstEvenAfterRestart()
    {
        string directory = Path.Combine(Path.GetTempPath(), "StilltimeTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "history.json");
        try
        {
            TimerHistoryStore store = new TimerHistoryStore(path);
            TimerHistoryEntry[] entries = Enumerable.Range(0, 10)
                .Select(i => NewEntry((TimerMode)(i % 4))).ToArray();
            foreach (TimerHistoryEntry entry in entries)
                store.Upsert(entry);
            store.Upsert(entries[0] with { StartedAtUtc = DateTimeOffset.UtcNow });
            Assert.AreEqual(10, store.Load().Count);
            Assert.AreEqual(entries[0].Id, store.Load()[0].Id);

            TimerHistoryEntry next = NewEntry(TimerMode.Stopwatch);
            store.Upsert(next);
            IReadOnlyList<TimerHistoryEntry> reopened = new TimerHistoryStore(path).Load();
            CollectionAssert.AreEqual(entries.Skip(1).Append(next).Select(e => e.Id).ToArray(),
                reopened.Select(e => e.Id).ToArray());
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void LoadingOlderHistoryPersistsOnlyLastTenEntries()
    {
        string directory = Path.Combine(Path.GetTempPath(), "StilltimeTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "history.json");
        try
        {
            Directory.CreateDirectory(directory);
            TimerHistoryEntry[] entries = Enumerable.Range(0, 20).Select(_ => NewEntry(TimerMode.Stopwatch)).ToArray();
            File.WriteAllText(path, JsonSerializer.Serialize(entries));
            IReadOnlyList<TimerHistoryEntry> loaded = new TimerHistoryStore(path).Load();
            CollectionAssert.AreEqual(entries.Skip(10).ToArray(), loaded.ToArray());
            Assert.AreEqual(10, JsonSerializer.Deserialize<List<TimerHistoryEntry>>(File.ReadAllText(path))!.Count);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(10)]
    public void InvalidEntriesAreRejectedBeforeTrimmingOrWriting(int invalidKind)
    {
        string directory = Path.Combine(Path.GetTempPath(), "StilltimeTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "history.json");
        TimerHistoryEntry[] entries = Enumerable.Range(0, 11).Select(_ => NewEntry(TimerMode.Stopwatch)).ToArray();
        entries[0] = invalidKind switch
        {
            0 => null!,
            1 => entries[0] with {Id = Guid.Empty},
            2 => entries[0] with {Id = entries[1].Id},
            3 => entries[0] with {Mode = (TimerMode)999},
            4 => entries[0] with {Status = (TimerHistoryStatus)999},
            5 => entries[0] with {Elapsed = TimeSpan.FromSeconds(-1)},
            6 => entries[0] with {Preparation = TimeSpan.FromSeconds(-1)},
            7 => entries[0] with {Work = TimeSpan.FromSeconds(-1)},
            8 => entries[0] with {Rest = TimeSpan.FromSeconds(-1)},
            9 => entries[0] with {Target = TimeSpan.FromSeconds(-1)},
            _ => entries[0] with {Rounds = -1}
        };
        string json = JsonSerializer.Serialize(entries);

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, json);
            TimerHistoryStore store = new TimerHistoryStore(path);

            Assert.ThrowsExactly<InvalidDataException>(() => store.Load());
            Assert.ThrowsExactly<InvalidDataException>(() => store.Upsert(NewEntry(TimerMode.Stopwatch)));
            Assert.ThrowsExactly<InvalidDataException>(() => store.Delete(entries[1].Id));
            Assert.AreEqual(json, File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static TimerHistoryEntry NewEntry(TimerMode mode) => new(
        Guid.NewGuid(),
        mode,
        new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero),
        TimerHistoryStatus.Running,
        TimeSpan.Zero);
}
