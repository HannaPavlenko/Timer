namespace Timer.Core;

/// <summary>Identifies a timer's audible cue.</summary>
public enum TimerSound
{
    /// <summary>A single completion tone.</summary>
    Completion,
    /// <summary>One preparation beep.</summary>
    Prepare,
    /// <summary>Two work beeps.</summary>
    Work,
    /// <summary>Three rest beeps.</summary>
    Rest,
    /// <summary>A resonant final gong.</summary>
    Gong
}

/// <summary>Carries the requested audio cue.</summary>
public sealed class TimerSoundEventArgs(TimerSound sound) : EventArgs
{
    /// <summary>Gets the cue to play.</summary>
    public TimerSound Sound { get; } = sound;
}
