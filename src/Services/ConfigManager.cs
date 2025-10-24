using Newtonsoft.Json;
using ServiceStatusBot.Models;

namespace ServiceStatusBot.Services;

/// <summary>
///     Loads the application's configuration from <c>config/config.json</c> and watches the file for changes.
///     Consumers can subscribe to the <see cref="ConfigChanged" /> event to react to runtime updates.
/// </summary>
public class ConfigManager
{
    private readonly string _configPath = Path.Combine(AppContext.BaseDirectory ?? ".", "config", "config.json");
    private readonly object _reloadLock = new();
    private DateTime _lastReloadTime = DateTime.MinValue;
    private readonly FileSystemWatcher? _watcher;

    public ConfigManager()
    {
        LoadConfigSafe();
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            _watcher = new FileSystemWatcher(dir, Path.GetFileName(_configPath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _watcher.Changed += (s, e) =>
            {
                lock (_reloadLock)
                {
                    // Debounce: ignore rapid successive changes within 1 second
                    var now = DateTime.Now;
                    if ((now - _lastReloadTime).TotalMilliseconds < 1000)
                        return;

                    _lastReloadTime = now;

                    try
                    {
                        // Delay to allow file write to complete
                        Thread.Sleep(100);
                        LoadConfigSafe();
                        ConfigChanged?.Invoke();
                        ErrorHelper.Log("Config reloaded successfully.");
                    }
                    catch (Exception ex)
                    {
                        ErrorHelper.LogError("Error reloading config", ex);
                    }
                }
            };
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            ErrorHelper.LogError("Failed to initialize FileSystemWatcher for config", ex);
        }
    }

    public Config Config { get; private set; } = new();
    public event Action? ConfigChanged;

    private void LoadConfigSafe()
    {
        try
        {
            if (File.Exists(_configPath))
                using (var stream = new FileStream(_configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var config = JsonConvert.DeserializeObject<Config>(json);
                    if (config == null)
                        throw new Exception("Config file is empty or invalid.");

                    // Validate critical fields
                    if (config.PollIntervalSeconds < 1)
                    {
                        ErrorHelper.LogWarning(
                            $"PollIntervalSeconds ({config.PollIntervalSeconds}) is too low; setting to 5 seconds minimum.");
                        config.PollIntervalSeconds = 5;
                    }

                    if (config.Services != null)
                        foreach (var svc in config.Services)
                        {
                            if (string.IsNullOrWhiteSpace(svc.Name))
                                ErrorHelper.LogWarning(
                                    "Service with empty Name found in config; it will be skipped at runtime.");
                            if (string.IsNullOrWhiteSpace(svc.Type))
                                ErrorHelper.LogWarning(
                                    $"Service '{svc.Name}' has no Type specified; it will fail checks.");
                        }

                    Config = config;
                }
        }
        catch (Exception ex)
        {
            ErrorHelper.LogError("Error loading config", ex);
            // Keep previous config if available
        }
    }
}