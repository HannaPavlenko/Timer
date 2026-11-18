using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Timer.Core;

namespace Timer.Storage;

/// <summary>Keeps history usable in memory after a storage failure for the rest of this app session.</summary>
internal sealed class SessionHistoryStore : ITimerHistoryStore
{
    private readonly Action _reportFailure;
    private readonly ITimerHistoryStore _store;
    private List<TimerHistoryEntry> _entries = [];
    private bool _storageUnavailable;

    /// <summary>Wraps a store and reports when persistence becomes unavailable.</summary>
    public SessionHistoryStore(ITimerHistoryStore store, Action reportFailure)
    {
        _store = store;
        _reportFailure = reportFailure;
    }

    /// <inheritdoc />
    public IReadOnlyList<TimerHistoryEntry> Load()
    {
        if (!_storageUnavailable)
        {
            try
            {
                _entries = _store.Load().ToList();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                             InvalidDataException)
            {
                DisableStorage();
            }
        }

        return _entries.ToArray();
    }

    /// <inheritdoc />
    public void Upsert(TimerHistoryEntry entry)
    {
        if (!_storageUnavailable)
        {
            try
            {
                _store.Upsert(entry);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                             InvalidDataException)
            {
                DisableStorage();
            }
        }

        int index = _entries.FindIndex(existing => existing.Id == entry.Id);
        if (index < 0)
            _entries.Add(entry);
        else
            _entries[index] = entry;

        if (_entries.Count > 10)
            _entries.RemoveRange(0, _entries.Count - 10);
    }

    /// <inheritdoc />
    public bool Delete(Guid id)
    {
        if (!_storageUnavailable)
        {
            try
            {
                bool deleted = _store.Delete(id);
                _entries.RemoveAll(entry => entry.Id == id);
                return deleted;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                             InvalidDataException)
            {
                DisableStorage();
            }
        }

        return _entries.RemoveAll(entry => entry.Id == id) > 0;
    }

    private void DisableStorage()
    {
        _storageUnavailable = true;
        _reportFailure();
    }
}
