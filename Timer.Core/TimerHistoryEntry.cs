namespace Timer.Core;

public sealed record TimerHistoryEntry(
    Guid Id,
    TimerMode Mode,
    DateTimeOffset StartedAtUtc,
    TimerHistoryStatus Status,
    TimeSpan Elapsed,
    TimeSpan? Target = null,
    TimeSpan Preparation = default,
    TimeSpan Work = default,
    TimeSpan Rest = default,
    int Rounds = 0,
    DateTimeOffset? EndedAtUtc = null,
    string? Name = null);
