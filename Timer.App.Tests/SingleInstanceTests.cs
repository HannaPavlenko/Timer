using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timer.Core;

namespace Timer.App.Tests;

/// <summary>Checks Windows startup exclusion and recovery using independent processes and isolated history.</summary>
[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class SingleInstanceTests
{
    /// <summary>A normal shutdown releases ownership for the next launch.</summary>
    [TestMethod]
    public async Task NextProcessCanEnterAfterNormalExit()
    {
        using TestHistory history = new TestHistory();

        using ProbeProcess owner = new ProbeProcess("hold", history);
        Assert.AreEqual("entered", await owner.ReadOutcomeAsync());
        owner.RequestExit();
        Assert.AreEqual(0, await owner.WaitForExitAsync());

        using ProbeProcess next = new ProbeProcess("probe", history);
        Assert.AreEqual("entered", await next.ReadOutcomeAsync());
        Assert.AreEqual(0, await next.WaitForExitAsync());
    }

    /// <summary>An abandoned mutex can be acquired and the previous process's running history recovered.</summary>
    [TestMethod]
    public async Task NextProcessRecoversAfterOwnerIsTerminated()
    {
        using TestHistory history = new TestHistory();

        using ProbeProcess owner = new ProbeProcess("hold", history);
        Assert.AreEqual("entered", await owner.ReadOutcomeAsync());

        // Keep the kernel object alive so the next launch encounters an abandoned mutex, not a new one.
        using Mutex observer = new Mutex(false, history.MutexName,
            new NamedWaitHandleOptions {CurrentUserOnly = true, CurrentSessionOnly = false});

        owner.Kill();
        await owner.WaitForExitAsync();

        using ProbeProcess next = new ProbeProcess("history", history);
        Assert.AreEqual("entered", await next.ReadOutcomeAsync());
        Assert.AreEqual(0, await next.WaitForExitAsync());

        TimerHistoryEntry recovered = new TimerHistoryStore(history.FilePath).Load().Single();
        Assert.AreEqual(history.EntryId, recovered.Id);
        Assert.AreEqual(TimerHistoryStatus.Stopped, recovered.Status);
        Assert.AreEqual(TimeSpan.FromSeconds(3), recovered.Elapsed);
    }

    /// <summary>A rejected process cannot normalize or otherwise change the running owner's history.</summary>
    [TestMethod]
    public async Task SecondProcessCannotEnterOrChangeHistory()
    {
        using TestHistory history = new TestHistory();

        using ProbeProcess owner = new ProbeProcess("hold", history);
        Assert.AreEqual("entered", await owner.ReadOutcomeAsync());

        using ProbeProcess second = new ProbeProcess("history", history);
        Assert.AreEqual("blocked", await second.ReadOutcomeAsync());
        Assert.AreEqual(2, await second.WaitForExitAsync());

        Assert.IsFalse(owner.HasExited);
        Assert.AreEqual(history.OriginalJson, File.ReadAllText(history.FilePath));
    }

    /// <summary>A failed startup releases the mutex even while the failing process is still alive.</summary>
    [TestMethod]
    public async Task StartupExceptionReleasesOwnership()
    {
        using TestHistory history = new TestHistory();

        using ProbeProcess failing = new ProbeProcess("throw", history);
        Assert.AreEqual("failed", await failing.ReadOutcomeAsync());
        Assert.IsFalse(failing.HasExited);

        using ProbeProcess next = new ProbeProcess("probe", history);
        Assert.AreEqual("entered", await next.ReadOutcomeAsync());
        Assert.AreEqual(0, await next.WaitForExitAsync());
        Assert.AreEqual(history.OriginalJson, File.ReadAllText(history.FilePath));

        failing.RequestExit();
        Assert.AreEqual(3, await failing.WaitForExitAsync());
    }

    private sealed class ProbeProcess : IDisposable
    {
        private readonly Process _process;
        private readonly Task<string> _standardError;

        public ProbeProcess(string mode, TestHistory history)
        {
            ProcessStartInfo start = new ProcessStartInfo("dotnet")
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            string testAssemblyPath = typeof(SingleInstanceTests).Assembly.Location;

            start.ArgumentList.Add("exec");
            start.ArgumentList.Add("--runtimeconfig");
            start.ArgumentList.Add(Path.ChangeExtension(testAssemblyPath, ".runtimeconfig.json"));
            start.ArgumentList.Add("--depsfile");
            start.ArgumentList.Add(Path.ChangeExtension(testAssemblyPath, ".deps.json"));
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Timer.Desktop.TestHost.dll"));
            start.ArgumentList.Add(mode);
            start.ArgumentList.Add(history.MutexName);
            start.ArgumentList.Add(history.FilePath);

            _process = Process.Start(start) ?? throw new InvalidOperationException("Test host did not start.");
            _standardError = _process.StandardError.ReadToEndAsync();
        }

        public bool HasExited => _process.HasExited;

        public void Dispose()
        {
            if (!_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(10000);
            }

            _process.Dispose();
        }

        public void Kill() => _process.Kill();

        public async Task<string> ReadOutcomeAsync()
        {
            string? outcome = await _process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
            if (outcome is not null)
                return outcome;

            string error = await _standardError.WaitAsync(TimeSpan.FromSeconds(20));
            throw new InvalidOperationException($"Test host exited without a result: {error}");
        }

        public void RequestExit() => _process.StandardInput.WriteLine();

        public async Task<int> WaitForExitAsync()
        {
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            return _process.ExitCode;
        }
    }

    private sealed class TestHistory : IDisposable
    {
        private readonly string _directoryPath = Path.Combine(Path.GetTempPath(), "StilltimeTests",
            Guid.NewGuid().ToString("N"));

        public TestHistory()
        {
            Directory.CreateDirectory(_directoryPath);

            TimerHistoryEntry entry = new TimerHistoryEntry(EntryId, TimerMode.Stopwatch, DateTimeOffset.UtcNow,
                TimerHistoryStatus.Running, TimeSpan.FromSeconds(3));

            OriginalJson = JsonSerializer.Serialize(new[] {entry});
            File.WriteAllText(FilePath, OriginalJson);
        }

        public Guid EntryId { get; } = Guid.NewGuid();
        public string FilePath => Path.Combine(_directoryPath, "history.json");
        public string MutexName { get; } = $"Stilltime.Tests.{Guid.NewGuid():N}";
        public string OriginalJson { get; }

        public void Dispose() => Directory.Delete(_directoryPath, recursive: true);
    }
}
