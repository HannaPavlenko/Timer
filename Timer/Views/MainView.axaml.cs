using Avalonia.Controls;
using System;
using Avalonia;
using Avalonia.Controls.Platform;
using Avalonia.Threading;
using Timer.ViewModels;

namespace Timer.Views;

public partial class MainView : UserControl
{
    private IInputPane? _inputPane;

    public MainView()
    {
        InitializeComponent();

        SizeChanged += (_, args) => TimeDisplayPanel.Height = args.NewSize.Height / 3;
        AttachedToVisualTree += (_, _) => (DataContext as MainViewModel)?.ResumeUpdates();
        DetachedFromVisualTree += (_, _) => (DataContext as MainViewModel)?.SuspendUpdates();

        if (OperatingSystem.IsAndroid())
        {
            AttachedToVisualTree += (_, _) =>
            {
                _inputPane = TopLevel.GetTopLevel(this)?.InputPane;
                if (_inputPane is not null)
                    _inputPane.StateChanged += InputPaneChanged;
            };
            DetachedFromVisualTree += (_, _) =>
            {
                if (_inputPane is not null)
                    _inputPane.StateChanged -= InputPaneChanged;

                _inputPane = null;
                FormScroll.Margin = default;
            };
            SizeChanged += (_, _) => UpdateKeyboardLayout();
        }
    }

    private void InputPaneChanged(object? sender, InputPaneStateEventArgs args) =>
        Dispatcher.UIThread.Post(UpdateKeyboardLayout, DispatcherPriority.Loaded);

    private void UpdateKeyboardLayout()
    {
        TopLevel? window = TopLevel.GetTopLevel(this);
        if (window is null)
            return;

        double bottom = this.TranslatePoint(new Point(0, Bounds.Height), window)?.Y ?? 0;
        double overlap = _inputPane is {State: InputPaneState.Open} pane && pane.OccludedRect.Height > 0
            ? Math.Clamp(bottom - pane.OccludedRect.Y, 0, Bounds.Height)
            : 0;
        FormScroll.Margin = new Thickness(0, 0, 0, overlap);

        if (window.FocusManager?.GetFocusedElement() is TextBox field)
            Dispatcher.UIThread.Post(() => field.BringIntoView(), DispatcherPriority.Loaded);
    }
}
