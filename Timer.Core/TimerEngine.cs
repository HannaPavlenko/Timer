namespace Timer.Core;

/// <summary>
/// Tracks active time with a monotonic timestamp. UI refresh frequency does not affect accuracy.
/// </summary>
public sealed class TimerEngine
{
    private readonly TimeProvider _timeProvider;
    private TimerMode _mode = TimerMode.Stopwatch;
    private TimerStatus _status = TimerStatus.Ready;
    private TimeSpan _duration;
    private TimeSpan _work;
    private TimeSpan _rest;
    private int _rounds;
    private TimeSpan _elapsedBeforeRun;
    private long _startedAt;

    public TimerEngine(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void ConfigureStopwatch() => Configure(TimerMode.Stopwatch);

    public void ConfigureCountUp(TimeSpan duration)
    {
        RequirePositive(duration, nameof(duration));
        Configure(TimerMode.CountUp);
        _duration = duration;
    }

    public void ConfigureCountdown(TimeSpan duration)
    {
        RequirePositive(duration, nameof(duration));
        Configure(TimerMode.Countdown);
        _duration = duration;
    }

    public void ConfigureIntervals(TimeSpan work, TimeSpan rest, int rounds)
    {
        RequirePositive(work, nameof(work));
        RequirePositive(rest, nameof(rest));
        if (rounds < 1)
            throw new ArgumentOutOfRangeException(nameof(rounds), "At least one round is required.");

        try
        {
            _ = checked(work.Ticks + rest.Ticks);
            _ = checked(work.Ticks * rounds + rest.Ticks * (rounds - 1L));
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(rounds), "The session is too long.");
        }

        Configure(TimerMode.Intervals);
        _work = work;
        _rest = rest;
        _rounds = rounds;
    }

    public void Start()
    {
        if (_status == TimerStatus.Running)
            return;

        if (_status == TimerStatus.Completed)
            _elapsedBeforeRun = TimeSpan.Zero;

        _startedAt = _timeProvider.GetTimestamp();
        _status = TimerStatus.Running;
    }

    public void Pause()
    {
        if (_status != TimerStatus.Running)
            return;

        _elapsedBeforeRun = CurrentElapsed();
        _status = TimerStatus.Paused;
    }

    public void Reset()
    {
        _elapsedBeforeRun = TimeSpan.Zero;
        _status = TimerStatus.Ready;
    }

    public TimerSnapshot Update()
    {
        var totalElapsed = CurrentElapsed();
        var totalDuration = SessionDuration();

        if (totalDuration is { } limit && totalElapsed >= limit)
        {
            totalElapsed = limit;
            _elapsedBeforeRun = limit;
            _status = TimerStatus.Completed;
        }

        if (_mode == TimerMode.Stopwatch)
            return new(_mode, _status, IntervalPhase.None, 0, 0,
                totalElapsed, TimeSpan.Zero, null, totalElapsed, null);

        if (_mode is TimerMode.CountUp or TimerMode.Countdown)
            return new(_mode, _status, IntervalPhase.None, 0, 0,
                totalElapsed, _duration - totalElapsed, _duration, totalElapsed, _duration);

        return IntervalSnapshot(totalElapsed, totalDuration!.Value);
    }

    private TimerSnapshot IntervalSnapshot(TimeSpan totalElapsed, TimeSpan totalDuration)
    {
        if (_status == TimerStatus.Completed)
            return new(_mode, _status, IntervalPhase.Complete, _rounds, _rounds,
                _work, TimeSpan.Zero, _work, totalElapsed, totalDuration);

        var cycleTicks = checked(_work.Ticks + _rest.Ticks);
        var completedCycles = totalElapsed.Ticks / cycleTicks;
        var offsetTicks = totalElapsed.Ticks % cycleTicks;
        var round = checked((int)completedCycles + 1);
        var isWork = offsetTicks < _work.Ticks;
        var phaseDuration = isWork ? _work : _rest;
        var phaseElapsed = isWork
            ? TimeSpan.FromTicks(offsetTicks)
            : TimeSpan.FromTicks(offsetTicks - _work.Ticks);

        return new(_mode, _status, isWork ? IntervalPhase.Work : IntervalPhase.Rest,
            round, _rounds, phaseElapsed, phaseDuration - phaseElapsed,
            phaseDuration, totalElapsed, totalDuration);
    }

    private TimeSpan CurrentElapsed() => _status == TimerStatus.Running
        ? _elapsedBeforeRun + _timeProvider.GetElapsedTime(_startedAt)
        : _elapsedBeforeRun;

    private TimeSpan? SessionDuration() => _mode switch
    {
        TimerMode.Stopwatch => null,
        TimerMode.CountUp or TimerMode.Countdown => _duration,
        TimerMode.Intervals => TimeSpan.FromTicks(
            checked(_work.Ticks * _rounds + _rest.Ticks * (_rounds - 1L))),
        _ => throw new InvalidOperationException("Unknown timer mode.")
    };

    private void Configure(TimerMode mode)
    {
        _mode = mode;
        _status = TimerStatus.Ready;
        _elapsedBeforeRun = TimeSpan.Zero;
    }

    private static void RequirePositive(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(parameterName, "Duration must be positive.");
    }
}
