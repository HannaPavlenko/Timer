using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timer.Core;

namespace Timer.Core.Tests;

[TestClass]
public sealed class TimerHistorySessionTests
{
    [TestMethod]
    public void PauseResumeAndCompletionUpdateOneEntry()
    {
        var store = new MemoryStore();
        var history = new TimerHistorySession(store);
        var entry = NewEntry();
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
        var store = new MemoryStore();
        var history = new TimerHistorySession(store);
        var first = NewEntry();
        history.StartNew(first);
        history.Stop(Snapshot(TimerStatus.Running, TimeSpan.FromSeconds(4)));

        var second = NewEntry();
        history.StartNew(second);
        Assert.AreEqual(2, store.Load().Count);
        Assert.AreEqual(TimerHistoryStatus.Stopped, store.Load()[0].Status);
        Assert.AreEqual(TimeSpan.FromSeconds(4), store.Load()[0].Elapsed);
        Assert.AreEqual(TimerHistoryStatus.Running, store.Load()[1].Status);
    }

    [TestMethod]
    public void PreviousProcessRunningEntriesBecomeStopped()
    {
        var store = new MemoryStore();
        var previous = NewEntry();
        store.Upsert(previous);

        _ = new TimerHistorySession(store);

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

        public IReadOnlyList<TimerHistoryEntry> Load() => _entries.ToArray();

        public void Upsert(TimerHistoryEntry entry)
        {
            var index = _entries.FindIndex(existing => existing.Id == entry.Id);
            if (index < 0)
                _entries.Add(entry);
            else
                _entries[index] = entry;
            WriteCount++;
        }

        public bool Delete(Guid id) => _entries.RemoveAll(entry => entry.Id == id) > 0;
    }
}
