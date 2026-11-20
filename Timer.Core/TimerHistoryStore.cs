using System.Text.Json;

namespace Timer.Core;

public sealed class TimerHistoryStore : ITimerHistoryStore
{
    private const int MaximumEntries = 10;
    private readonly Func<string?> _readJson;
    private readonly Action<string> _writeJson;
    private readonly object _sync = new();

    /// <summary>Uses the application history path unless an isolated path is supplied.</summary>
    public TimerHistoryStore(string? filePath = null)
    {
        string path = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Stilltime",
            "history.json");

        _readJson = () => StorageFile.ReadTextIfPresent(path);
        _writeJson = json => StorageFile.WriteTextAtomically(path, "history", json);
    }

    /// <summary>Stores history through platform-provided JSON storage.</summary>
    public TimerHistoryStore(Func<string?> readJson, Action<string> writeJson)
    {
        _readJson = readJson;
        _writeJson = writeJson;
    }

    public IReadOnlyList<TimerHistoryEntry> Load()
    {
        lock (_sync)
        {
            List<TimerHistoryEntry> entries = ReadEntries();
            if (TrimOldest(entries))
                WriteEntries(entries);
            return entries;
        }
    }

    public void Upsert(TimerHistoryEntry entry)
    {
        lock (_sync)
        {
            List<TimerHistoryEntry> entries = ReadEntries();
            int index = entries.FindIndex(existing => existing.Id == entry.Id);
            if (index < 0)
                entries.Add(entry);
            else
                entries[index] = entry;

            TrimOldest(entries);
            WriteEntries(entries);
        }
    }

    public bool Delete(Guid id)
    {
        lock (_sync)
        {
            List<TimerHistoryEntry> entries = ReadEntries();
            if (entries.RemoveAll(entry => entry.Id == id) == 0)
                return false;

            WriteEntries(entries);
            return true;
        }
    }

    private List<TimerHistoryEntry> ReadEntries()
    {
        string? json = _readJson();
        if (json is null)
            return [];

        try
        {
            List<TimerHistoryEntry> entries = JsonSerializer.Deserialize(json, StorageJsonContext.Default.ListTimerHistoryEntry)
                ?? throw new InvalidDataException("Timer history cannot be empty JSON.");
            HashSet<Guid> ids = [];
            foreach (TimerHistoryEntry entry in entries)
            {
                if (entry is null || entry.Id == Guid.Empty || !ids.Add(entry.Id) ||
                    !Enum.IsDefined(entry.Mode) || !Enum.IsDefined(entry.Status) ||
                    entry.Elapsed < TimeSpan.Zero || entry.Preparation < TimeSpan.Zero ||
                    entry.Work < TimeSpan.Zero || entry.Rest < TimeSpan.Zero ||
                    entry.Target < TimeSpan.Zero || entry.Rounds < 0)
                    throw new InvalidDataException("Timer history contains invalid or duplicate entries.");
            }

            return entries;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Timer history contains invalid JSON.", exception);
        }
    }

    private static bool TrimOldest(List<TimerHistoryEntry> entries)
    {
        if (entries.Count <= MaximumEntries)
            return false;

        entries.RemoveRange(0, entries.Count - MaximumEntries);
        return true;
    }

    private void WriteEntries(List<TimerHistoryEntry> entries)
    {
        _writeJson(JsonSerializer.Serialize(entries, StorageJsonContext.Default.ListTimerHistoryEntry));
    }
}
