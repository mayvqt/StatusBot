using System;
using System;
using Serilog;

namespace StatusBot.Services;

/// <summary>
/// Lightweight logging helper that routes to Serilog's static logger. If Serilog is not yet
/// configured the methods will catch exceptions to avoid bringing down the host during startup.
/// </summary>
internal static class ErrorHelper
{
    /// <summary>Log an informational message.</summary>
    public static void Log(string message)
    {
        try
        {
            Serilog.Log.Information(message);
        }
        catch
        {
            // In early startup Serilog may not be ready; swallow failures to avoid crashing the host.
        }
    }

    /// <summary>Log a warning message.</summary>
    public static void LogWarning(string message)
    {
        try
        {
            Serilog.Log.Warning(message);
        }
        catch
        {
            // Swallow; we're best-effort here.
        }
    }

    /// <summary>Log an error message with an optional exception.</summary>
    public static void LogError(string message, Exception? ex = null)
    {
        try
        {
            if (ex != null) Serilog.Log.Error(ex, message);
            else Serilog.Log.Error(message);
        }
        catch
        {
            // Swallow to avoid recursive failures during logger setup.
        }
    }

    /// <summary>
    /// Execute a function and return a fallback value on exception. Errors are logged.
    /// </summary>
    public static T? Safe<T>(Func<T> func, T? fallback = default)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            LogError("Unhandled exception in Safe wrapper", ex);
            return fallback;
        }
    }

    /// <summary>Execute an action and log any uncaught exceptions.</summary>
    public static void Safe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LogError("Unhandled exception in Safe wrapper", ex);
        }
    }
}
