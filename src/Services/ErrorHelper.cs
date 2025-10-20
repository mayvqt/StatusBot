using System;
using System;
using Serilog;

namespace ServiceStatusBot.Services;

internal static class ErrorHelper
{
    // Use Serilog static logger directly. If Serilog isn't configured yet, fallback to Console.
    public static void Log(string message)
    {
        try
        {
            Serilog.Log.Information(message);
        }
        catch
        {
            Console.WriteLine($"[Info] {DateTime.UtcNow:O} - {message}");
        }
    }

    public static void LogWarning(string message)
    {
        try
        {
            Serilog.Log.Warning(message);
        }
        catch
        {
            Console.WriteLine($"[Warn] {DateTime.UtcNow:O} - {message}");
        }
    }

    public static void LogError(string message, Exception? ex = null)
    {
        try
        {
            if (ex != null) Serilog.Log.Error(ex, message);
            else Serilog.Log.Error(message);
        }
        catch
        {
            Console.WriteLine($"[Error] {DateTime.UtcNow:O} - {message}");
            if (ex != null) Console.WriteLine(ex.ToString());
        }
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
