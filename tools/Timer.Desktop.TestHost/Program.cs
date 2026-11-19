using Timer.Core;
using Timer.Desktop;

if (!OperatingSystem.IsWindows())
    return 4;

if (args.Length != 3 || args[0] is not ("hold" or "probe" or "history" or "throw"))
{
    Console.Error.WriteLine("Usage: Timer.Desktop.TestHost <hold|probe|history|throw> <mutex-name> <history-path>");
    return 4;
}

string mode = args[0];
try
{
    bool entered = SingleInstanceApplication.TryRun(args[1], () =>
    {
        if (mode == "throw")
            throw new InvalidOperationException("Simulated startup failure.");

        if (mode == "history")
            _ = new TimerHistorySession(new TimerHistoryStore(args[2]));

        Console.WriteLine("entered");

        if (mode == "hold")
            Console.ReadLine();
    });

    if (!entered)
    {
        Console.WriteLine("blocked");
        return 2;
    }

    return 0;
}
catch (InvalidOperationException) when (mode == "throw")
{
    Console.WriteLine("failed");
    Console.ReadLine();
    return 3;
}
