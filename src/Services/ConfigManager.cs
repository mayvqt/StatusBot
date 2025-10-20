using Newtonsoft.Json;
using ServiceStatusBot.Models;

namespace ServiceStatusBot.Services;

/// <summary>
/// Loads the application's configuration from <c>config/config.json</c> and watches the file for changes.
/// Consumers can subscribe to the <see cref="ConfigChanged"/> event to react to runtime updates.
/// </summary>
public class ConfigManager
{
    public Config Config { get; private set; } = new();
    private readonly string _configPath = Path.Combine(AppContext.BaseDirectory ?? ".", "config", "config.json");
    private FileSystemWatcher? _watcher;
    public event Action? ConfigChanged;

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
                try
                {
                    LoadConfigSafe();
                    ConfigChanged?.Invoke();
                    ErrorHelper.Log("Config reloaded successfully.");
                }
                catch (Exception ex)
                {
                    ErrorHelper.LogError("Error reloading config", ex);
                }
            };
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            ErrorHelper.LogError("Failed to initialize FileSystemWatcher for config", ex);
        }
    }

    private void LoadConfigSafe()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                using (var stream = new FileStream(_configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var config = JsonConvert.DeserializeObject<Config>(json);
                    if (config == null)
                        throw new Exception("Config file is empty or invalid.");
                    Config = config;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorHelper.LogError("Error loading config", ex);
            // Keep previous config if available
        }
    }
}
