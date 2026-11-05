using System;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timer.Core;

namespace Timer.ViewModels;

public sealed class TimerHistoryItemViewModel : ObservableObject
{
    private readonly string _startedLabel;
    private string _timeAndDateLabel = string.Empty;
    private string _name;
    private readonly Action<Guid, string> _rename;

    public TimerHistoryItemViewModel(TimerHistoryEntry entry, Action<Guid> delete,
        Action<TimerHistoryEntry> restart, Action<Guid> stop, bool canRestart, bool canStop,
        Action<Guid, string> rename)
    {
        Id = entry.Id;
        _rename = rename;
        _name = entry.Name ?? string.Empty;
        ModeLabel = entry.Mode switch
        {
            TimerMode.Stopwatch => "Stopwatch",
            TimerMode.CountUp => "Count up",
            TimerMode.Countdown => "Countdown",
            TimerMode.Intervals => "Intervals",
            _ => throw new ArgumentOutOfRangeException(nameof(entry))
        };
        StatusLabel = entry.Status switch
        {
            TimerHistoryStatus.Running => "Running",
            TimerHistoryStatus.Paused => "Paused",
            TimerHistoryStatus.Completed => "Completed",
            TimerHistoryStatus.Stopped => "Stopped",
            _ => throw new ArgumentOutOfRangeException(nameof(entry))
        };
        _startedLabel = entry.StartedAtUtc.ToLocalTime().ToString("MMM d, yyyy · HH:mm", CultureInfo.InvariantCulture);
        UpdateElapsed(entry.Elapsed);
        SettingsLabel = entry.Mode switch
        {
            TimerMode.CountUp or TimerMode.Countdown => $"Target {FormatTime(entry.Target ?? TimeSpan.Zero)}",
            TimerMode.Intervals => $"Prepare {FormatTime(entry.Preparation)} · Work {FormatTime(entry.Work)} · Rest {FormatTime(entry.Rest)} · {entry.Rounds} rounds",
            _ => string.Empty
        };
        CanRestart = canRestart;
        CanStop = canStop;
        ShowStatus = !CanRestart && !CanStop;
        RestartCommand = new RelayCommand(() => restart(entry));
        StopCommand = new RelayCommand(() => stop(entry.Id));
        DeleteCommand = new RelayCommand(() => delete(entry.Id));
    }

    public Guid Id { get; }
    public string ModeLabel { get; }
    public string Name
    {
        get => _name;
        set
        {
            var name = value ?? string.Empty;
            if (SetProperty(ref _name, name))
                _rename(Id, name);
        }
    }
    public string StatusLabel { get; }
    public bool CanRestart { get; }
    public bool CanStop { get; }
    public bool ShowStatus { get; }
    public string TimeAndDateLabel
    {
        get => _timeAndDateLabel;
        private set => SetProperty(ref _timeAndDateLabel, value);
    }
    public string SettingsLabel { get; }
    public bool HasSettingsLabel => !string.IsNullOrEmpty(SettingsLabel);
    public ICommand RestartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand DeleteCommand { get; }

    public void UpdateElapsed(TimeSpan elapsed) =>
        TimeAndDateLabel = $"{FormatTime(elapsed)} · {_startedLabel}";

    private static string FormatTime(TimeSpan time)
    {
        var totalSeconds = (long)time.TotalSeconds;
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        var seconds = totalSeconds % 60;
        return hours > 0
            ? $"{hours:00}:{minutes:00}:{seconds:00}"
            : $"{minutes:00}:{seconds:00}";
    }
}
