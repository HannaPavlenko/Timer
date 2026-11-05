namespace Timer.Core;

public sealed class TimerHistorySession
{
    private readonly ITimerHistoryStore _store;
    private TimerHistoryEntry? _current;

    public Guid? CurrentEntryId => _current?.Id;

    public TimerHistorySession(ITimerHistoryStore store)
    {
        _store = store;

        // A previous process cannot still own a running timer session.
        foreach (var entry in store.Load())
        {
            if (entry.Status == TimerHistoryStatus.Running)
                store.Upsert(entry with { Status = TimerHistoryStatus.Stopped });
        }
    }

    public void StartNew(TimerHistoryEntry entry)
    {
        if (_current is not null)
            throw new InvalidOperationException("Stop the current history entry before starting another.");
        if (entry.Status != TimerHistoryStatus.Running)
            throw new ArgumentException("A new history entry must be running.", nameof(entry));

        _store.Upsert(entry);
        _current = entry;
    }

    public void Restart(TimerHistoryEntry entry)
    {
        if (_current is not null)
            throw new InvalidOperationException("Stop the current history entry before restarting another.");
        if (entry.Status is not (TimerHistoryStatus.Stopped or TimerHistoryStatus.Paused))
            throw new ArgumentException("Only paused or stopped entries can restart.", nameof(entry));

        var restarted = entry with
        {
            StartedAtUtc = DateTimeOffset.UtcNow,
            Status = TimerHistoryStatus.Running,
            Elapsed = TimeSpan.Zero,
            EndedAtUtc = null
        };
        _store.Upsert(restarted);
        _current = restarted;
    }

    public void Update(TimerSnapshot snapshot)
    {
        if (_current is null)
            return;

        var status = snapshot.Status switch
        {
            TimerStatus.Running => TimerHistoryStatus.Running,
            TimerStatus.Paused => TimerHistoryStatus.Paused,
            TimerStatus.Completed => TimerHistoryStatus.Completed,
            _ => TimerHistoryStatus.Stopped
        };

        if (status == _current.Status)
            return;

        _current = _current with
        {
            Status = status,
            Elapsed = snapshot.TotalElapsed,
            EndedAtUtc = status == TimerHistoryStatus.Completed ? DateTimeOffset.UtcNow : null
        };
        _store.Upsert(_current);
        if (status is TimerHistoryStatus.Completed or TimerHistoryStatus.Stopped)
            _current = null;
    }

    public void Stop(TimerSnapshot snapshot)
    {
        if (_current is null)
            return;

        var status = snapshot.Status == TimerStatus.Completed
            ? TimerHistoryStatus.Completed
            : TimerHistoryStatus.Stopped;
        _current = _current with
        {
            Status = status,
            Elapsed = snapshot.TotalElapsed,
            EndedAtUtc = DateTimeOffset.UtcNow
        };
        _store.Upsert(_current);
        _current = null;
    }

    public bool Delete(Guid id)
    {
        var wasCurrent = _current?.Id == id;
        if (wasCurrent)
            _current = null;
        var deleted = _store.Delete(id);
        return deleted || wasCurrent;
    }
}
