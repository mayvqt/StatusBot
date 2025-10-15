using System;

namespace ServiceStatusBot.Services;

internal static class ErrorHelper
{
    // Centralized simple logging to console for now.
    public static void Log(string message)
    {
        Console.WriteLine($"[Info] {DateTime.UtcNow:O} - {message}");
    }

    public static void LogWarning(string message)
    {
        Console.WriteLine($"[Warn] {DateTime.UtcNow:O} - {message}");
    }

    public static void LogError(string message, Exception? ex = null)
    {
        Console.WriteLine($"[Error] {DateTime.UtcNow:O} - {message}");
        if (ex != null)
        {
            Console.WriteLine(ex.ToString());
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
