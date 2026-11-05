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
        var directory = Path.Combine(Path.GetTempPath(), "StilltimeTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "history.json");

        try
        {
            var store = new TimerHistoryStore(path);
            Assert.AreEqual(0, store.Load().Count);

            var first = NewEntry(TimerMode.CountUp) with { Target = TimeSpan.FromSeconds(6) };
            var second = NewEntry(TimerMode.Intervals) with
            {
                Preparation = TimeSpan.Zero,
                Work = TimeSpan.FromSeconds(45),
                Rest = TimeSpan.FromSeconds(15),
                Rounds = 3
            };

            store.Upsert(first);
            store.Upsert(second);

            var reopened = new TimerHistoryStore(path);
            CollectionAssert.AreEqual(new[] { first, second }, reopened.Load().ToArray());

            var paused = first with
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
        var directory = Path.Combine(Path.GetTempPath(), "StilltimeTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "history.json");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{invalid json");
            var store = new TimerHistoryStore(path);

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
        var directory = Path.Combine(Path.GetTempPath(), "StilltimeTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "history.json");
        try
        {
            var store = new TimerHistoryStore(path);
            var entries = Enumerable.Range(0, 10)
                .Select(i => NewEntry((TimerMode)(i % 4))).ToArray();
            foreach (var entry in entries)
                store.Upsert(entry);
            store.Upsert(entries[0] with { StartedAtUtc = DateTimeOffset.UtcNow });
            Assert.AreEqual(10, store.Load().Count);
            Assert.AreEqual(entries[0].Id, store.Load()[0].Id);

            var next = NewEntry(TimerMode.Stopwatch);
            store.Upsert(next);
            var reopened = new TimerHistoryStore(path).Load();
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
        var directory = Path.Combine(Path.GetTempPath(), "StilltimeTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "history.json");
        try
        {
            Directory.CreateDirectory(directory);
            var entries = Enumerable.Range(0, 20).Select(_ => NewEntry(TimerMode.Stopwatch)).ToArray();
            File.WriteAllText(path, JsonSerializer.Serialize(entries));
            var loaded = new TimerHistoryStore(path).Load();
            CollectionAssert.AreEqual(entries.Skip(10).ToArray(), loaded.ToArray());
            Assert.AreEqual(10, JsonSerializer.Deserialize<List<TimerHistoryEntry>>(File.ReadAllText(path))!.Count);
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
