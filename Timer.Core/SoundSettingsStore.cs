using System.Text.Json;

namespace Timer.Core;

/// <summary>Stores the sound preference in a local JSON file.</summary>
public sealed class SoundSettingsStore : ISoundSettingsStore
{
    private readonly string _filePath;

    /// <summary>Uses the application settings path unless an isolated path is supplied.</summary>
    public SoundSettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Stilltime", "sound.json");
    }

    /// <inheritdoc />
    public bool Load()
    {
        string? json = StorageFile.ReadTextIfPresent(_filePath);
        if (json is null)
            return false;

        try
        {
            return JsonSerializer.Deserialize<bool>(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Sound settings contain invalid JSON.", exception);
        }
    }

    /// <inheritdoc />
    public void Save(bool enabled)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(_filePath))!;
        Directory.CreateDirectory(directory);

        string temporaryPath = Path.Combine(directory, $"sound-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(enabled));
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
