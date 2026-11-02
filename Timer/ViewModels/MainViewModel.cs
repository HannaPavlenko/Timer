using System;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timer.Core;

namespace Timer.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly TimerEngine _engine = new();
    private readonly DispatcherTimer _refreshTimer;
    private TimerMode _selectedMode;
    private TimerStatus _status = TimerStatus.Ready;

    public MainViewModel()
    {
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _refreshTimer.Tick += (_, _) => Refresh();
        SelectMode(TimerMode.Stopwatch);
    }

    public bool IsStopwatchSelected => _selectedMode == TimerMode.Stopwatch;
    public bool IsCountUpSelected => _selectedMode == TimerMode.CountUp;
    public bool IsCountdownSelected => _selectedMode == TimerMode.Countdown;
    public bool IsIntervalsSelected => _selectedMode == TimerMode.Intervals;
    public bool IsDurationMode => _selectedMode is TimerMode.CountUp or TimerMode.Countdown;
    public bool CanEditSettings => _status is TimerStatus.Ready or TimerStatus.Completed;

    [ObservableProperty] private string _durationMinutes = "5";
    [ObservableProperty] private string _durationSeconds = "0";
    [ObservableProperty] private string _workMinutes = "0";
    [ObservableProperty] private string _workSeconds = "45";
    [ObservableProperty] private string _restMinutes = "0";
    [ObservableProperty] private string _restSeconds = "15";
    [ObservableProperty] private string _rounds = "4";
    [ObservableProperty] private string _modeTitle = "Stopwatch";
    [ObservableProperty] private string _modeDescription = "Keep time without a finish line.";
    [ObservableProperty] private string _displayTime = "00:00.0";
    [ObservableProperty] private string _statusLabel = "READY";
    [ObservableProperty] private string _phaseLabel = "ELAPSED TIME";
    [ObservableProperty] private string _detailLabel = "Start whenever you're ready.";
    [ObservableProperty] private string _primaryActionLabel = "Start timer";
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private bool _hasProgress;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnDurationMinutesChanged(string value) => RefreshReadyPreview();
    partial void OnDurationSecondsChanged(string value) => RefreshReadyPreview();
    partial void OnWorkMinutesChanged(string value) => RefreshReadyPreview();
    partial void OnWorkSecondsChanged(string value) => RefreshReadyPreview();
    partial void OnRestMinutesChanged(string value) => RefreshReadyPreview();
    partial void OnRestSecondsChanged(string value) => RefreshReadyPreview();
    partial void OnRoundsChanged(string value) => RefreshReadyPreview();
    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    [RelayCommand] private void SelectStopwatch() => SelectMode(TimerMode.Stopwatch);
    [RelayCommand] private void SelectCountUp() => SelectMode(TimerMode.CountUp);
    [RelayCommand] private void SelectCountdown() => SelectMode(TimerMode.Countdown);
    [RelayCommand] private void SelectIntervals() => SelectMode(TimerMode.Intervals);

    [RelayCommand]
    private void StartOrPause()
    {
        if (_status == TimerStatus.Running)
        {
            _engine.Pause();
            _refreshTimer.Stop();
            Refresh();
            return;
        }

        if (_status != TimerStatus.Paused && !TryConfigure())
            return;

        _engine.Start();
        _refreshTimer.Start();
        Refresh();
    }

    [RelayCommand]
    private void Reset()
    {
        _refreshTimer.Stop();
        _engine.Reset();
        _status = TimerStatus.Ready;
        PrepareMode();
    }

    public void SuspendUpdates() => _refreshTimer.Stop();

    public void ResumeUpdates()
    {
        Refresh();
        if (_status == TimerStatus.Running)
            _refreshTimer.Start();
    }

    private void SelectMode(TimerMode mode)
    {
        _refreshTimer.Stop();
        _selectedMode = mode;
        _status = TimerStatus.Ready;
        ModeTitle = mode switch
        {
            TimerMode.Stopwatch => "Stopwatch",
            TimerMode.CountUp => "Count up",
            TimerMode.Countdown => "Countdown",
            TimerMode.Intervals => "Intervals",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        ModeDescription = mode switch
        {
            TimerMode.Stopwatch => "Keep time without a finish line.",
            TimerMode.CountUp => "Watch time grow toward your goal.",
            TimerMode.Countdown => "Give a moment a clear ending.",
            TimerMode.Intervals => "Move between focus and rest.",
            _ => string.Empty
        };

        OnPropertyChanged(nameof(IsStopwatchSelected));
        OnPropertyChanged(nameof(IsCountUpSelected));
        OnPropertyChanged(nameof(IsCountdownSelected));
        OnPropertyChanged(nameof(IsIntervalsSelected));
        OnPropertyChanged(nameof(IsDurationMode));
        OnPropertyChanged(nameof(CanEditSettings));
        PrepareMode();
    }

    private void RefreshReadyPreview()
    {
        if (_status is TimerStatus.Ready or TimerStatus.Completed)
            PrepareMode();
    }

    private void PrepareMode()
    {
        if (TryConfigure())
            Refresh();
        else
        {
            DisplayTime = "--:--.-";
            StatusLabel = "READY";
            PhaseLabel = "SET YOUR TIME";
            DetailLabel = "Check the values below.";
            PrimaryActionLabel = "Start timer";
            ProgressPercent = 0;
        }
    }

    private bool TryConfigure()
    {
        ErrorMessage = string.Empty;

        if (_selectedMode == TimerMode.Stopwatch)
        {
            _engine.ConfigureStopwatch();
            return true;
        }

        if (_selectedMode is TimerMode.CountUp or TimerMode.Countdown)
        {
            if (!TryDuration(DurationMinutes, DurationSeconds, out var duration))
            {
                ErrorMessage = "Enter a duration from 1 second to 999 minutes 59 seconds.";
                return false;
            }

            if (_selectedMode == TimerMode.CountUp)
                _engine.ConfigureCountUp(duration);
            else
                _engine.ConfigureCountdown(duration);
            return true;
        }

        if (!TryDuration(WorkMinutes, WorkSeconds, out var work) ||
            !TryDuration(RestMinutes, RestSeconds, out var rest) ||
            !int.TryParse(Rounds, NumberStyles.None, CultureInfo.InvariantCulture, out var rounds) ||
            rounds is < 1 or > 99)
        {
            ErrorMessage = "Enter valid work and rest times, and 1 to 99 rounds.";
            return false;
        }

        _engine.ConfigureIntervals(work, rest, rounds);
        return true;
    }

    private void Refresh()
    {
        var snapshot = _engine.Update();
        _status = snapshot.Status;
        if (_status == TimerStatus.Completed)
            _refreshTimer.Stop();

        DisplayTime = FormatTime(snapshot.Mode switch
        {
            TimerMode.Countdown or TimerMode.Intervals => snapshot.Remaining,
            _ => snapshot.Elapsed
        });
        StatusLabel = snapshot.Status switch
        {
            TimerStatus.Ready => "READY",
            TimerStatus.Running => "RUNNING",
            TimerStatus.Paused => "PAUSED",
            TimerStatus.Completed => "FINISHED",
            _ => string.Empty
        };
        PhaseLabel = snapshot.Mode switch
        {
            TimerMode.Stopwatch => "ELAPSED TIME",
            TimerMode.CountUp => "TIME ELAPSED",
            TimerMode.Countdown => "TIME REMAINING",
            TimerMode.Intervals => snapshot.Phase switch
            {
                IntervalPhase.Work => "WORK INTERVAL",
                IntervalPhase.Rest => "REST INTERVAL",
                _ => "SESSION COMPLETE"
            },
            _ => string.Empty
        };
        DetailLabel = snapshot.Mode switch
        {
            TimerMode.Stopwatch => snapshot.Status == TimerStatus.Ready
                ? "Start whenever you're ready."
                : "A little time, well spent.",
            TimerMode.CountUp => $"Goal  {FormatTime(snapshot.Target!.Value)}",
            TimerMode.Countdown => $"From  {FormatTime(snapshot.Target!.Value)}",
            TimerMode.Intervals => $"Round {snapshot.Round} of {snapshot.Rounds}  ·  " +
                (snapshot.Phase == IntervalPhase.Rest ? "rest" :
                 snapshot.Phase == IntervalPhase.Complete ? "complete" : "work"),
            _ => string.Empty
        };
        PrimaryActionLabel = snapshot.Status switch
        {
            TimerStatus.Running => "Pause timer",
            TimerStatus.Paused => "Resume timer",
            TimerStatus.Completed => "Start again",
            _ => "Start timer"
        };
        HasProgress = snapshot.Target is not null;
        ProgressPercent = snapshot.Progress * 100;
        OnPropertyChanged(nameof(CanEditSettings));
    }

    private static bool TryDuration(string minutesText, string secondsText, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (!int.TryParse(minutesText, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            !int.TryParse(secondsText, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            minutes is < 0 or > 999 || seconds is < 0 or > 59 ||
            minutes == 0 && seconds == 0)
            return false;

        duration = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        return true;
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";

        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds / 100}";
    }
}
