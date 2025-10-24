using Newtonsoft.Json;
using ServiceStatusBot.Models;

namespace ServiceStatusBot.Services;

/// <summary>
///     Helper used at startup to ensure the <c>config</c> directory and default config/state files exist.
///     This is a convenience for first-time runs and tests; in production you may provide your own files.
/// </summary>
public static class SetupHelper
{
    /// <summary>
    ///     Ensure default configuration and state files exist. If missing, write simple defaults.
    /// </summary>
    public static void EnsureConfigAndState()
    {
        try
        {
            var configDir = Path.Combine(AppContext.BaseDirectory ?? ".", "config");
            Directory.CreateDirectory(configDir);
            var configPath = Path.Combine(configDir, "config.json");
            if (!File.Exists(configPath))
            {
                var defaultConfig = new Config
                {
                    Token = "YOUR_DISCORD_BOT_TOKEN",
                    ChannelId = 123456789012345678,
                    // Optional default presence text shown by the bot. Empty = auto-detect first HTTP host.
                    PresenceText = "",
                    PollIntervalSeconds = 60,
                    Services = new List<ServiceDefinition>
                    {
                        new() { Name = "MainSite", Type = "HTTP", Url = "https://example.com" },
                        new() { Name = "API", Type = "TCP", Host = "api.example.com", Port = 443 },
                        new() { Name = "DNS", Type = "ICMP", Host = "8.8.8.8" }
                    }
                };
                File.WriteAllText(configPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
            }

            var statePath = Path.Combine(AppContext.BaseDirectory ?? ".", "config", "state.json");
            if (!File.Exists(statePath))
            {
                // Read the config to seed initial statuses for each configured service.
                Config? cfg = null;
                try
                {
                    var cfgJson = File.ReadAllText(configPath);
                    cfg = JsonConvert.DeserializeObject<Config>(cfgJson) ?? new Config();
                }
                catch
                {
                    cfg = new Config();
                }

                var initialStatuses = new Dictionary<string, ServiceStatus>();
                var now = DateTime.Now;
                foreach (var svc in cfg.Services ?? new List<ServiceDefinition>())
                {
                    if (string.IsNullOrWhiteSpace(svc.Name)) continue;
                    initialStatuses[svc.Name] = new ServiceStatus
                    {
                        Online = false,
                        LastChecked = now,
                        LastChange = now,
                        MonitoringSince = now,
                        CumulativeUpSeconds = 0.0,
                        TotalChecks = 0,
                        UptimePercent = 0.0
                    };
                }

                var defaultState = new State
                {
                    StatusMessageId = 0,
                    StatusMessageLastUpdatedUtc = default,
                    Statuses = initialStatuses
                    ,
                    // Explicitly set the schema version for clarity
                    Version = "2"
                };

                File.WriteAllText(statePath, JsonConvert.SerializeObject(defaultState, Formatting.Indented));
            }
        }
        catch (Exception ex)
        {
            ErrorHelper.LogError("Error ensuring config and state files", ex);
            // Best-effort continue; ConfigManager and Persistence will handle missing files
        }
    }
}