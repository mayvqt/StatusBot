using System;
using System;
using Serilog;

namespace ServiceStatusBot.Services;

internal static class ErrorHelper
{
    // Use Serilog static logger directly. If Serilog isn't configured yet, fallback to Console.
    public static void Log(string message)
    {
        Serilog.Log.Information(message);
    }

    public static void LogWarning(string message)
    {
        Serilog.Log.Warning(message);
    }

    public static void LogError(string message, Exception? ex = null)
    {
        if (ex != null) Serilog.Log.Error(ex, message);
        else Serilog.Log.Error(message);
    }

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
