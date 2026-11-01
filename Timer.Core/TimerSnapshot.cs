namespace Timer.Core;

public sealed record TimerSnapshot(
    TimerMode Mode,
    TimerStatus Status,
    IntervalPhase Phase,
    int Round,
    int Rounds,
    TimeSpan Elapsed,
    TimeSpan Remaining,
    TimeSpan? Target,
    TimeSpan TotalElapsed,
    TimeSpan? TotalDuration)
{
    public double Progress => Target is { Ticks: > 0 } target
        ? Math.Clamp(Elapsed.Ticks / (double)target.Ticks, 0, 1)
        : 0;
}
