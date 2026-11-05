using System;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Timer.Core;

namespace Timer.ViewModels;

public sealed class TimerHistoryItemViewModel
{
    public TimerHistoryItemViewModel(TimerHistoryEntry entry, Action<Guid> delete)
    {
        Id = entry.Id;
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
        var started = entry.StartedAtUtc.ToLocalTime().ToString("MMM d, yyyy · HH:mm", CultureInfo.InvariantCulture);
        TimeAndDateLabel = $"{FormatTime(entry.Elapsed)} · {started}";
        SettingsLabel = entry.Mode switch
        {
            TimerMode.CountUp or TimerMode.Countdown => $"Target {FormatTime(entry.Target ?? TimeSpan.Zero)}",
            TimerMode.Intervals => $"Prepare {FormatTime(entry.Preparation)} · Work {FormatTime(entry.Work)} · Rest {FormatTime(entry.Rest)} · {entry.Rounds} rounds",
            _ => string.Empty
        };
        DeleteCommand = new RelayCommand(() => delete(entry.Id));
    }

    public Guid Id { get; }
    public string ModeLabel { get; }
    public string StatusLabel { get; }
    public string TimeAndDateLabel { get; }
    public string SettingsLabel { get; }
    public bool HasSettingsLabel => !string.IsNullOrEmpty(SettingsLabel);
    public ICommand DeleteCommand { get; }

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
