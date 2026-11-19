using System;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading;

namespace Timer.Desktop;

/// <summary>Runs one Windows desktop instance per user across all login sessions.</summary>
[SupportedOSPlatform("windows")]
internal static class SingleInstanceApplication
{
    /// <summary>Acquires the Stilltime user mutex before initializing application state.</summary>
    public static bool TryRun(Action run)
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string userId = identity.User?.Value ?? throw new InvalidOperationException("Windows user SID is unavailable.");
        return TryRun($"Stilltime.Desktop.{userId}", run);
    }

    /// <summary>Runs synchronously under a named mutex, or returns false when another process owns it.</summary>
    public static bool TryRun(string mutexName, Action run)
    {
        ArgumentNullException.ThrowIfNull(run);

        using Mutex mutex = new Mutex(false, mutexName,
            new NamedWaitHandleOptions {CurrentUserOnly = true, CurrentSessionOnly = false});

        try
        {
            if (!mutex.WaitOne(0))
                return false;
        }
        catch (AbandonedMutexException)
        {
            // WaitOne grants ownership when the previous owning process terminated unexpectedly.
        }

        try
        {
            run();
            return true;
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
}
