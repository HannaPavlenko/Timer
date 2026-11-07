using Avalonia.Controls;
using Timer.ViewModels;

namespace Timer.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        SizeChanged += (_, args) => TimeDisplayPanel.Height = args.NewSize.Height / 3;
        AttachedToVisualTree += (_, _) => (DataContext as MainViewModel)?.ResumeUpdates();
        DetachedFromVisualTree += (_, _) => (DataContext as MainViewModel)?.SuspendUpdates();
    }
}
