using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Timer.Core;
using Timer.ViewModels;
using Timer.Views;

namespace Timer;

public partial class App : Application
{
    /// <summary>Platform audio configured by the host before Avalonia creates any views.</summary>
    public static SoundPlayback? SoundPlayback { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = CreateViewModel()
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime activityLifetime)
        {
            activityLifetime.MainViewFactory = () => new MainView
            {
                DataContext = CreateViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = CreateViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static MainViewModel CreateViewModel()
    {
        var viewModel = new MainViewModel();
        viewModel.SoundRequested += async (_, _) =>
        {
            if (SoundPlayback is { } playback)
                await playback.PlayAsync();
        };

        return viewModel;
    }
}
