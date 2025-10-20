using ServiceStatusBot.Models;
using Newtonsoft.Json;

namespace ServiceStatusBot.Services;

/// <summary>
/// Helper used at startup to ensure the <c>config</c> directory and default config/state files exist.
/// This is a convenience for first-time runs and tests; in production you may provide your own files.
/// </summary>
public static class SetupHelper
{
    /// <summary>
    /// Ensure default configuration and state files exist. If missing, write simple defaults.
    /// </summary>
    public static void EnsureConfigAndState()
    {
        try
        {
            Directory.CreateDirectory("config");
            var configPath = Path.Combine("config", "config.json");
            if (!File.Exists(configPath))
            {
                var defaultConfig = new Config
                {
                    Token = "YOUR_DISCORD_BOT_TOKEN",
                    ChannelId = 123456789012345678,
                    PollIntervalSeconds = 60,
                    Services = new List<ServiceDefinition>
                    {
                        new ServiceDefinition { Name = "MainSite", Type = "HTTP", Url = "https://example.com" },
                        new ServiceDefinition { Name = "API", Type = "TCP", Host = "api.example.com", Port = 443 },
                        new ServiceDefinition { Name = "DNS", Type = "ICMP", Host = "8.8.8.8" }
                    }
                };
                File.WriteAllText(configPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
            }

            var statePath = Path.Combine("config", "state.json");
            if (!File.Exists(statePath))
            {
                var defaultState = new State
                {
                    MessageMetadata = new Dictionary<string, MessageReference>(),
                    Statuses = new Dictionary<string, ServiceStatus>()
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
