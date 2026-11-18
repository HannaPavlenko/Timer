using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timer.Core;

namespace Timer.Core.Tests;

[TestClass]
public sealed class TimerHistorySessionTests
{
    [TestMethod]
    public void PauseResumeAndCompletionUpdateOneEntry()
    {
        MemoryStore store = new MemoryStore();
        TimerHistorySession history = new TimerHistorySession(store);
        TimerHistoryEntry entry = NewEntry();
        history.StartNew(entry);
        history.Update(Snapshot(TimerStatus.Running, TimeSpan.FromSeconds(2)));
        Assert.AreEqual(1, store.WriteCount);

        history.Update(Snapshot(TimerStatus.Paused, TimeSpan.FromSeconds(3)));
        Assert.AreEqual(TimerHistoryStatus.Paused, store.Load()[0].Status);
        Assert.AreEqual(TimeSpan.FromSeconds(3), store.Load()[0].Elapsed);

        history.Update(Snapshot(TimerStatus.Running, TimeSpan.FromSeconds(3)));
        history.Update(Snapshot(TimerStatus.Completed, TimeSpan.FromSeconds(6)));
        Assert.AreEqual(TimerHistoryStatus.Completed, store.Load()[0].Status);
        Assert.AreEqual(TimeSpan.FromSeconds(6), store.Load()[0].Elapsed);
        Assert.IsNotNull(store.Load()[0].EndedAtUtc);
        Assert.AreEqual(4, store.WriteCount);
    }

    [TestMethod]
    public void ResetStopsCurrentEntryAndAllowsAnotherStart()
    {
        MemoryStore store = new MemoryStore();
        TimerHistorySession history = new TimerHistorySession(store);
        TimerHistoryEntry first = NewEntry();
        history.StartNew(first);
        history.Stop(Snapshot(TimerStatus.Running, TimeSpan.FromSeconds(4)));

        TimerHistoryEntry second = NewEntry();
        history.StartNew(second);
        Assert.AreEqual(2, store.Load().Count);
        Assert.AreEqual(TimerHistoryStatus.Stopped, store.Load()[0].Status);
        Assert.AreEqual(TimeSpan.FromSeconds(4), store.Load()[0].Elapsed);
        Assert.AreEqual(TimerHistoryStatus.Running, store.Load()[1].Status);
    }

    [TestMethod]
    public void PreviousProcessRunningEntriesBecomeStopped()
    {
        MemoryStore store = new MemoryStore();
        TimerHistoryEntry previous = NewEntry();
        store.Upsert(previous);

        _ = new TimerHistorySession(store);

        Assert.AreEqual(TimerHistoryStatus.Stopped, store.Load()[0].Status);
    }

    [TestMethod]
    public void DeletedRunningEntryIsNotRestoredByLaterUpdates()
    {
        MemoryStore store = new MemoryStore();
        TimerHistorySession history = new TimerHistorySession(store);
        TimerHistoryEntry entry = NewEntry();
        history.StartNew(entry);

        Assert.IsTrue(history.Delete(entry.Id));
        history.Update(Snapshot(TimerStatus.Paused, TimeSpan.FromSeconds(2)));
        history.Stop(Snapshot(TimerStatus.Paused, TimeSpan.FromSeconds(2)));

        Assert.AreEqual(0, store.Load().Count);
    }

    [TestMethod]
    public void RestartResetsAndReusesTheSameEntry()
    {
        MemoryStore store = new MemoryStore();
        TimerHistorySession history = new TimerHistorySession(store);
        TimerHistoryEntry previous = NewEntry() with
        {
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            Status = TimerHistoryStatus.Stopped,
            Elapsed = TimeSpan.FromSeconds(4),
            EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-4)
        };
        store.Upsert(previous);

        history.Restart(previous);

        TimerHistoryEntry running = store.Load()[0];
        Assert.AreEqual(1, store.Load().Count);
        Assert.AreEqual(previous.Id, running.Id);
        Assert.AreEqual(TimerHistoryStatus.Running, running.Status);
        Assert.AreEqual(TimeSpan.Zero, running.Elapsed);
        Assert.IsNull(running.EndedAtUtc);
        Assert.IsTrue(running.StartedAtUtc > previous.StartedAtUtc);
        Assert.AreEqual(previous.Id, history.CurrentEntryId);

        history.Stop(Snapshot(TimerStatus.Running, TimeSpan.FromSeconds(2)));
        Assert.AreEqual(1, store.Load().Count);
        Assert.AreEqual(TimerHistoryStatus.Stopped, store.Load()[0].Status);
    }

    [TestMethod]
    public void FailedDeleteKeepsCurrentEntryAttached()
    {
        MemoryStore store = new MemoryStore();
        TimerHistorySession history = new TimerHistorySession(store);
        TimerHistoryEntry entry = NewEntry();
        history.StartNew(entry);
        store.FailDelete = true;

        Assert.ThrowsExactly<IOException>(() => history.Delete(entry.Id));
        Assert.AreEqual(entry.Id, history.CurrentEntryId);

        history.Stop(Snapshot(TimerStatus.Running, TimeSpan.FromSeconds(2)));

        Assert.AreEqual(TimerHistoryStatus.Stopped, store.Load()[0].Status);
        Assert.AreEqual(TimeSpan.FromSeconds(2), store.Load()[0].Elapsed);
    }

    [TestMethod]
    [DataRow(TimerStatus.Paused)]
    [DataRow(TimerStatus.Completed)]
    public void FailedUpdateCanBeRetriedWithoutLosingTransition(TimerStatus status)
    {
        MemoryStore store = new MemoryStore();
        TimerHistorySession history = new TimerHistorySession(store);
        TimerHistoryEntry entry = NewEntry();
        history.StartNew(entry);
        store.FailWrites = true;
        TimerSnapshot snapshot = Snapshot(status, TimeSpan.FromSeconds(6));

        Assert.ThrowsExactly<IOException>(() => history.Update(snapshot));
        Assert.AreEqual(entry.Id, history.CurrentEntryId);
        Assert.AreEqual(TimerHistoryStatus.Running, store.Load()[0].Status);
        store.FailWrites = false;
        history.Update(snapshot);

        Assert.AreEqual(status == TimerStatus.Completed ? TimerHistoryStatus.Completed : TimerHistoryStatus.Paused,
            store.Load()[0].Status);
        Assert.AreEqual(TimeSpan.FromSeconds(6), store.Load()[0].Elapsed);
        Assert.AreEqual(status == TimerStatus.Completed ? (Guid?)null : entry.Id, history.CurrentEntryId);
    }

    [TestMethod]
    public void FailedStopCanBeRetried()
    {
        MemoryStore store = new MemoryStore();
        TimerHistorySession history = new TimerHistorySession(store);
        TimerHistoryEntry entry = NewEntry();
        history.StartNew(entry);
        store.FailWrites = true;

        Assert.ThrowsExactly<IOException>(() => history.Stop(Snapshot(TimerStatus.Running, TimeSpan.FromSeconds(2))));
        Assert.AreEqual(entry.Id, history.CurrentEntryId);
        store.FailWrites = false;
        history.Stop(Snapshot(TimerStatus.Running, TimeSpan.FromSeconds(3)));

        Assert.IsNull(history.CurrentEntryId);
        Assert.AreEqual(TimeSpan.FromSeconds(3), store.Load()[0].Elapsed);
        Assert.AreEqual(TimerHistoryStatus.Stopped, store.Load()[0].Status);
    }

    private static TimerHistoryEntry NewEntry() => new(
        Guid.NewGuid(), TimerMode.CountUp, DateTimeOffset.UtcNow,
        TimerHistoryStatus.Running, TimeSpan.Zero, TimeSpan.FromSeconds(6));

    private static TimerSnapshot Snapshot(TimerStatus status, TimeSpan elapsed) => new(
        TimerMode.CountUp, status, IntervalPhase.None, 0, 0,
        elapsed, TimeSpan.FromSeconds(6) - elapsed, TimeSpan.FromSeconds(6),
        elapsed, TimeSpan.FromSeconds(6));

    private sealed class MemoryStore : ITimerHistoryStore
    {
        private readonly List<TimerHistoryEntry> _entries = [];

        public int WriteCount { get; private set; }
        public bool FailWrites { get; set; }
        public bool FailDelete { get; set; }

        public IReadOnlyList<TimerHistoryEntry> Load() => _entries.ToArray();

        public void Upsert(TimerHistoryEntry entry)
        {
            if (FailWrites)
                throw new IOException("Simulated write failure.");

            int index = _entries.FindIndex(existing => existing.Id == entry.Id);
            if (index < 0)
                _entries.Add(entry);
            else
                _entries[index] = entry;
            WriteCount++;
        }

        public bool Delete(Guid id)
        {
            if (FailDelete)
                throw new IOException("Simulated delete failure.");

            return _entries.RemoveAll(entry => entry.Id == id) > 0;
        }
    }
}
