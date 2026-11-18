using System;
using System.IO;
using Timer.Core;

namespace Timer.Storage;

/// <summary>Keeps sound changes local to this app session after a settings file failure.</summary>
internal sealed class SessionSoundSettingsStore : ISoundSettingsStore
{
    private readonly Action _reportFailure;
    private readonly ISoundSettingsStore _store;
    private bool _enabled;
    private bool _storageUnavailable;

    /// <summary>Wraps a store and reports when persistence becomes unavailable.</summary>
    public SessionSoundSettingsStore(ISoundSettingsStore store, Action reportFailure)
    {
        _store = store;
        _reportFailure = reportFailure;
    }

    /// <inheritdoc />
    public bool Load()
    {
        if (!_storageUnavailable)
        {
            try
            {
                _enabled = _store.Load();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                             InvalidDataException)
            {
                DisableStorage();
            }
        }

        return _enabled;
    }

    /// <inheritdoc />
    public void Save(bool enabled)
    {
        if (!_storageUnavailable)
        {
            try
            {
                _store.Save(enabled);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                             InvalidDataException)
            {
                DisableStorage();
            }
        }

        _enabled = enabled;
    }

    private void DisableStorage()
    {
        _storageUnavailable = true;
        _reportFailure();
    }
}
