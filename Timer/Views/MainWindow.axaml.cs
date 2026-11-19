using Avalonia.Controls;
using Timer.ViewModels;

namespace Timer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as MainViewModel)?.SaveRunningTimerOnClose();
    }
}
