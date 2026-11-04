using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Timer.Controls;
using Timer.ViewModels;
using Timer.Views;

var verifyInput = args.Length == 1 && args[0] == "verify-input";
var width = 0;
if (!verifyInput && (args.Length != 3 || !int.TryParse(args[1], out width) || width < 320))
{
    Console.Error.WriteLine("Usage: Timer.Preview <stopwatch|countdown|intervals> <width> <output.png> | verify-input");
    return 1;
}

AppBuilder.Configure<Timer.App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

var viewModel = new MainViewModel();
if (verifyInput)
{
    viewModel.SelectCountUpCommand.Execute(null);
    var inputWindow = new Window
    {
        Width = 520,
        Height = 800,
        Content = new MainView { DataContext = viewModel }
    };
    inputWindow.Show();
    Dispatcher.UIThread.RunJobs();

    var inputs = inputWindow.GetVisualDescendants().OfType<DigitsOnlyTextBox>()
        .Take(2).ToArray();
    var success = inputs.Length == 2;
    foreach (var input in inputs)
    {
        input.Text = "";
        input.Focus();
        inputWindow.KeyTextInput("а");
        inputWindow.KeyTextInput("a");
        Dispatcher.UIThread.RunJobs();
        success &= input.Text == "";
        inputWindow.KeyTextInput("6");
        Dispatcher.UIThread.RunJobs();
        success &= input.Text == "6";
        if (OperatingSystem.IsWindows())
            success &= !InputMethod.GetIsInputMethodEnabled(input);
    }

    Console.WriteLine(success ? "Numeric inputs accept digits and reject letters." : "Numeric input verification failed.");
    inputWindow.Close();
    return success ? 0 : 1;
}

switch (args[0].ToLowerInvariant())
{
    case "stopwatch":
        break;
    case "countdown":
        viewModel.SelectCountdownCommand.Execute(null);
        break;
    case "intervals":
        viewModel.WorkSeconds = "45";
        viewModel.RestSeconds = "15";
        viewModel.Rounds = "4";
        viewModel.SelectIntervalsCommand.Execute(null);
        break;
    default:
        Console.Error.WriteLine("Unknown mode.");
        return 1;
}

var window = new Window
{
    Width = width,
    Height = 800,
    Content = new MainView { DataContext = viewModel }
};
window.Show();
Dispatcher.UIThread.RunJobs();
AvaloniaHeadlessPlatform.ForceRenderTimerTick();
var frame = window.CaptureRenderedFrame();
if (frame is null)
{
    Console.Error.WriteLine("The headless renderer did not produce a frame.");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
frame.Save(args[2], PngBitmapEncoderOptions.Default);
window.Close();
return 0;
