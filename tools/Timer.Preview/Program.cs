using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Timer.ViewModels;
using Timer.Views;

if (args.Length != 3 || !int.TryParse(args[1], out var width) || width < 320)
{
    Console.Error.WriteLine("Usage: Timer.Preview <stopwatch|countdown|intervals> <width> <output.png>");
    return 1;
}

AppBuilder.Configure<Timer.App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

var viewModel = new MainViewModel();
switch (args[0].ToLowerInvariant())
{
    case "stopwatch":
        break;
    case "countdown":
        viewModel.SelectCountdownCommand.Execute(null);
        break;
    case "intervals":
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
