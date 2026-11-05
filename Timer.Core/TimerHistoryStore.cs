using System.Text.Json;

namespace Timer.Core;

public sealed class TimerHistoryStore : ITimerHistoryStore
{
    private const int MaximumEntries = 15;
    private readonly string _filePath;
    private readonly object _sync = new();

    public TimerHistoryStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Stilltime",
            "history.json");
    }

    public IReadOnlyList<TimerHistoryEntry> Load()
    {
        lock (_sync)
        {
            var entries = ReadEntries();
            if (TrimOldest(entries))
                WriteEntries(entries);
            return entries;
        }
    }

    public void Upsert(TimerHistoryEntry entry)
    {
        lock (_sync)
        {
            var entries = ReadEntries();
            var index = entries.FindIndex(existing => existing.Id == entry.Id);
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
            var entries = ReadEntries();
            if (entries.RemoveAll(entry => entry.Id == id) == 0)
                return false;

            WriteEntries(entries);
            return true;
        }
    }

    private List<TimerHistoryEntry> ReadEntries()
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<TimerHistoryEntry>>(File.ReadAllText(_filePath))
                ?? throw new InvalidDataException("Timer history cannot be empty JSON.");
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
        var directory = Path.GetDirectoryName(Path.GetFullPath(_filePath))!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"history-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entries));
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
