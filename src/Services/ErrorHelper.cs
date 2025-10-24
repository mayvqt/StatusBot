namespace StatusBot.Services;

/// <summary>Safe logging wrapper with early-startup protection</summary>
internal static class ErrorHelper
{
    /// <summary>Log info message</summary>
    public static void Log(string message)
    {
        try
        {
            Serilog.Log.Information(message);
        }
        catch
        {
            // Ignore during startup
        }
    }

    /// <summary>Log warning message</summary>
    public static void LogWarning(string message)
    {
        try
        {
            Serilog.Log.Warning(message);
        }
        catch
        {
            // Ignore during startup
        }
    }

    /// <summary>Log error with optional exception</summary>
    public static void LogError(string message, Exception? ex = null)
    {
        try
        {
            if (ex != null) Serilog.Log.Error(ex, message);
            else Serilog.Log.Error(message);
        }
        catch
        {
            // Ignore during startup
        }
    }

    /// <summary>Run function with error logging</summary>
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

    /// <summary>Run action with error logging</summary>
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