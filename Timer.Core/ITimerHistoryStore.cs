namespace Timer.Core;

public interface ITimerHistoryStore
{
    IReadOnlyList<TimerHistoryEntry> Load();

    void Upsert(TimerHistoryEntry entry);

    bool Delete(Guid id);
}
