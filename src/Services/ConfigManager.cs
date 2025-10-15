using Newtonsoft.Json;
using ServiceStatusBot.Models;

namespace ServiceStatusBot.Services;

public class ConfigManager
{
    public Config Config { get; private set; } = new();
    private readonly string _configPath = "config.json";
    private FileSystemWatcher? _watcher;
    public event Action? ConfigChanged;

    public ConfigManager()
    {
        LoadConfig();
        var dir = Path.GetDirectoryName(_configPath);
        if (string.IsNullOrEmpty(dir)) dir = ".";
        _watcher = new FileSystemWatcher(dir, Path.GetFileName(_configPath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };
        _watcher.Changed += (s, e) =>
        {
            LoadConfig();
            ConfigChanged?.Invoke();
        };
        _watcher.EnableRaisingEvents = true;
    }

    public void LoadConfig()
    {
        if (File.Exists(_configPath))
        {
            var json = File.ReadAllText(_configPath);
            Config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
        }
    }
}
