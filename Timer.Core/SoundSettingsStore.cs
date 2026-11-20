using System.Text.Json;

namespace Timer.Core;

/// <summary>Stores the sound preference as JSON.</summary>
public sealed class SoundSettingsStore : ISoundSettingsStore
{
    private readonly Func<string?> _readJson;
    private readonly Action<string> _writeJson;

    /// <summary>Uses the application settings path unless an isolated path is supplied.</summary>
    public SoundSettingsStore(string? filePath = null)
    {
        string path = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Stilltime", "sound.json");

        _readJson = () => StorageFile.ReadTextIfPresent(path);
        _writeJson = json => StorageFile.WriteTextAtomically(path, "sound", json);
    }

    /// <summary>Stores sound settings through platform-provided JSON storage.</summary>
    public SoundSettingsStore(Func<string?> readJson, Action<string> writeJson)
    {
        _readJson = readJson;
        _writeJson = writeJson;
    }

    /// <inheritdoc />
    public bool Load()
    {
        string? json = _readJson();
        if (json is null)
            return false;

        try
        {
            return JsonSerializer.Deserialize(json, StorageJsonContext.Default.Boolean);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Sound settings contain invalid JSON.", exception);
        }
    }

    /// <inheritdoc />
    public void Save(bool enabled)
    {
        _writeJson(JsonSerializer.Serialize(enabled, StorageJsonContext.Default.Boolean));
    }
}
