using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timer.Core;

namespace Timer.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly TimerEngine _engine = new();
    private readonly ITimerHistoryStore _historyStore;
    private readonly TimerHistorySession _history;
    private readonly DispatcherTimer _refreshTimer;
    private TimerMode _selectedMode;
    private TimerStatus _status = TimerStatus.Ready;
    private (string Minutes, string Seconds) _countUpDuration;
    private (string Minutes, string Seconds) _countdownDuration;

    public MainViewModel() : this(new TimerHistoryStore())
    {
    }

    public MainViewModel(ITimerHistoryStore historyStore)
    {
        _historyStore = historyStore;
        _history = new TimerHistorySession(historyStore);
        _countUpDuration = (DurationMinutes, DurationSeconds);
        _countdownDuration = (DurationMinutes, DurationSeconds);
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _refreshTimer.Tick += (_, _) => Refresh();
        SelectMode(TimerMode.Stopwatch);
    }

    public int SelectedModeIndex
    {
        get => (int)_selectedMode;
        set
        {
            if (value is < 0 or > 3 || value == (int)_selectedMode)
                return;

            SelectMode((TimerMode)value);
        }
    }

    public bool IsIntervalsSelected => _selectedMode == TimerMode.Intervals;
    public bool IsDurationMode => _selectedMode is TimerMode.CountUp or TimerMode.Countdown;
    public bool CanEditSettings => _status is TimerStatus.Ready or TimerStatus.Completed;
    public ObservableCollection<TimerHistoryItemViewModel> HistoryItems { get; } = [];
    public bool HasNoHistory => HistoryItems.Count == 0;

    [ObservableProperty] private string _durationMinutes = "0";
    [ObservableProperty] private string _durationSeconds = "0";
    [ObservableProperty] private string _preparationMinutes = "0";
    [ObservableProperty] private string _preparationSeconds = "0";
    [ObservableProperty] private string _workMinutes = "0";
    [ObservableProperty] private string _workSeconds = "0";
    [ObservableProperty] private string _restMinutes = "0";
    [ObservableProperty] private string _restSeconds = "0";
    [ObservableProperty] private string _rounds = "0";
    [ObservableProperty] private string _modeTitle = "Stopwatch";
    [ObservableProperty] private string _displayTime = "00:00";
    [ObservableProperty] private string _statusLabel = "READY";
    [ObservableProperty] private string _phaseLabel = "ELAPSED TIME";
    [ObservableProperty] private string _detailLabel = "";
    [ObservableProperty] private string _primaryActionLabel = "Start timer";
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private bool _hasProgress;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasDetailLabel => !string.IsNullOrEmpty(DetailLabel);

    partial void OnDurationMinutesChanged(string value) => RefreshReadyPreview();
    partial void OnDurationSecondsChanged(string value) => RefreshReadyPreview();
    partial void OnPreparationMinutesChanged(string value) => RefreshReadyPreview();
    partial void OnPreparationSecondsChanged(string value) => RefreshReadyPreview();
    partial void OnWorkMinutesChanged(string value) => RefreshReadyPreview();
    partial void OnWorkSecondsChanged(string value) => RefreshReadyPreview();
    partial void OnRestMinutesChanged(string value) => RefreshReadyPreview();
    partial void OnRestSecondsChanged(string value) => RefreshReadyPreview();
    partial void OnRoundsChanged(string value) => RefreshReadyPreview();
    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnDetailLabelChanged(string value) => OnPropertyChanged(nameof(HasDetailLabel));

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
            ReloadHistory();
            return;
        }

        if (_status != TimerStatus.Paused && !TryConfigure())
            return;

        if (_status != TimerStatus.Paused)
            _history.StartNew(CreateHistoryEntry(_engine.Update()));

        _engine.Start();
        _refreshTimer.Start();
        Refresh();
        ReloadHistory();
    }

    [RelayCommand]
    private void Reset()
    {
        _refreshTimer.Stop();
        _history.Stop(_engine.Update());
        ReloadHistory();
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
        _history.Stop(_engine.Update());
        if (_selectedMode == TimerMode.CountUp)
            _countUpDuration = (DurationMinutes, DurationSeconds);
        else if (_selectedMode == TimerMode.Countdown)
            _countdownDuration = (DurationMinutes, DurationSeconds);

        _selectedMode = mode;
        _status = TimerStatus.Ready;
        if (mode is TimerMode.CountUp or TimerMode.Countdown)
        {
            var (minutes, seconds) = mode == TimerMode.CountUp
                ? _countUpDuration
                : _countdownDuration;
            DurationMinutes = minutes;
            DurationSeconds = seconds;
        }

        ModeTitle = mode switch
        {
            TimerMode.Stopwatch => "Stopwatch",
            TimerMode.CountUp => "Count up",
            TimerMode.Countdown => "Countdown",
            TimerMode.Intervals => "Intervals",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        OnPropertyChanged(nameof(SelectedModeIndex));
        OnPropertyChanged(nameof(IsIntervalsSelected));
        OnPropertyChanged(nameof(IsDurationMode));
        OnPropertyChanged(nameof(CanEditSettings));
        PrepareMode();
        ReloadHistory();
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
            DisplayTime = "00:00";
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

        if (!TryDuration(PreparationMinutes, PreparationSeconds, out var preparation, allowZero: true) ||
            !TryDuration(WorkMinutes, WorkSeconds, out var work) ||
            !TryDuration(RestMinutes, RestSeconds, out var rest) ||
            !int.TryParse(Rounds, NumberStyles.None, CultureInfo.InvariantCulture, out var rounds) ||
            rounds is < 1 or > 99)
        {
            ErrorMessage = "Enter valid preparation, work and rest times, and 1 to 99 rounds.";
            return false;
        }

        _engine.ConfigureIntervals(preparation, work, rest, rounds);
        return true;
    }

    private void Refresh()
    {
        var previousStatus = _status;
        var snapshot = _engine.Update();
        _history.Update(snapshot);
        _status = snapshot.Status;
        if (snapshot.Status == TimerStatus.Completed && previousStatus != TimerStatus.Completed)
            ReloadHistory();
        if (_status == TimerStatus.Completed)
            _refreshTimer.Stop();

        DisplayTime = FormatTime(snapshot.Mode switch
        {
            TimerMode.Countdown or TimerMode.Intervals => snapshot.Remaining,
            _ => snapshot.Elapsed
        }, roundUp: snapshot.Mode is TimerMode.Countdown or TimerMode.Intervals);
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
                IntervalPhase.Prepare => "GET READY",
                IntervalPhase.Work => "WORK INTERVAL",
                IntervalPhase.Rest => "REST INTERVAL",
                _ => "SESSION COMPLETE"
            },
            _ => string.Empty
        };
        DetailLabel = snapshot.Mode switch
        {
            TimerMode.Stopwatch => string.Empty,
            TimerMode.CountUp => $"Goal  {FormatTime(snapshot.Target!.Value)}",
            TimerMode.Countdown => $"From  {FormatTime(snapshot.Target!.Value)}",
            TimerMode.Intervals => snapshot.Phase == IntervalPhase.Prepare
                ? $"Prepare for round 1 of {snapshot.Rounds}"
                : $"Round {snapshot.Round} of {snapshot.Rounds}  ·  " +
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

    private void DeleteHistoryEntry(Guid id)
    {
        if (_history.Delete(id))
            ReloadHistory();
    }

    private void RestartHistoryEntry(TimerHistoryEntry entry)
    {
        if (!CanRestartHistoryEntry(entry))
            return;

        SelectMode(entry.Mode);
        if (entry.Mode is TimerMode.CountUp or TimerMode.Countdown)
        {
            (DurationMinutes, DurationSeconds) = HistoryDurationFields(entry.Target!.Value);
        }
        else if (entry.Mode == TimerMode.Intervals)
        {
            (PreparationMinutes, PreparationSeconds) = HistoryDurationFields(entry.Preparation);
            (WorkMinutes, WorkSeconds) = HistoryDurationFields(entry.Work);
            (RestMinutes, RestSeconds) = HistoryDurationFields(entry.Rest);
            Rounds = entry.Rounds.ToString(CultureInfo.InvariantCulture);
        }

        StartOrPause();
    }

    private static bool CanRestartHistoryEntry(TimerHistoryEntry entry)
    {
        if (entry.Status is not (TimerHistoryStatus.Stopped or TimerHistoryStatus.Paused))
            return false;

        return entry.Mode switch
        {
            TimerMode.Stopwatch => true,
            TimerMode.CountUp or TimerMode.Countdown => entry.Target is { } target &&
                IsValidHistoryDuration(target),
            TimerMode.Intervals => IsValidHistoryDuration(entry.Preparation, allowZero: true) &&
                IsValidHistoryDuration(entry.Work) && IsValidHistoryDuration(entry.Rest) &&
                entry.Rounds is >= 1 and <= 99,
            _ => false
        };
    }

    private static bool IsValidHistoryDuration(TimeSpan duration, bool allowZero = false) =>
        duration >= (allowZero ? TimeSpan.Zero : TimeSpan.FromSeconds(1)) &&
        duration <= TimeSpan.FromMinutes(999) + TimeSpan.FromSeconds(59) &&
        duration.Ticks % TimeSpan.TicksPerSecond == 0;

    private static (string Minutes, string Seconds) HistoryDurationFields(TimeSpan duration) =>
        (((int)duration.TotalMinutes).ToString(CultureInfo.InvariantCulture),
            duration.Seconds.ToString(CultureInfo.InvariantCulture));

    private void ReloadHistory()
    {
        HistoryItems.Clear();
        var entries = _historyStore.Load();
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            if (entries[index].Mode == _selectedMode)
                HistoryItems.Add(new TimerHistoryItemViewModel(entries[index], DeleteHistoryEntry,
                    RestartHistoryEntry, CanRestartHistoryEntry(entries[index])));
        }
        OnPropertyChanged(nameof(HasNoHistory));
    }

    private TimerHistoryEntry CreateHistoryEntry(TimerSnapshot snapshot)
    {
        var preparation = TimeSpan.Zero;
        var work = TimeSpan.Zero;
        var rest = TimeSpan.Zero;
        var rounds = 0;

        if (snapshot.Mode == TimerMode.Intervals)
        {
            _ = TryDuration(PreparationMinutes, PreparationSeconds, out preparation, allowZero: true);
            _ = TryDuration(WorkMinutes, WorkSeconds, out work);
            _ = TryDuration(RestMinutes, RestSeconds, out rest);
            rounds = int.Parse(Rounds, CultureInfo.InvariantCulture);
        }

        return new TimerHistoryEntry(
            Guid.NewGuid(),
            snapshot.Mode,
            DateTimeOffset.UtcNow,
            TimerHistoryStatus.Running,
            TimeSpan.Zero,
            snapshot.Mode is TimerMode.CountUp or TimerMode.Countdown ? snapshot.Target : null,
            preparation,
            work,
            rest,
            rounds);
    }

    private static bool TryDuration(string minutesText, string secondsText, out TimeSpan duration,
        bool allowZero = false)
    {
        duration = TimeSpan.Zero;
        minutesText = string.IsNullOrEmpty(minutesText) ? "0" : minutesText;
        secondsText = string.IsNullOrEmpty(secondsText) ? "0" : secondsText;
        if (!int.TryParse(minutesText, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            !int.TryParse(secondsText, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            minutes is < 0 or > 999 || seconds is < 0 or > 59 ||
            !allowZero && minutes == 0 && seconds == 0)
            return false;

        duration = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
        return true;
    }

    private static string FormatTime(TimeSpan time, bool roundUp = false)
    {
        var totalSeconds = time.Ticks / TimeSpan.TicksPerSecond;
        if (roundUp && time.Ticks % TimeSpan.TicksPerSecond != 0)
            totalSeconds++;

        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        var seconds = totalSeconds % 60;
        if (hours > 0)
            return $"{hours:00}:{minutes:00}:{seconds:00}";

        return $"{minutes:00}:{seconds:00}";
    }
}
