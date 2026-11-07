namespace Timer.Core;

/// <summary>Persists the shared sound preference independently of timer history.</summary>
public interface ISoundSettingsStore
{
    /// <summary>Returns the saved preference, or false when none has been saved.</summary>
    bool Load();

    /// <summary>Saves whether countdown completion sounds are enabled.</summary>
    void Save(bool enabled);
}
