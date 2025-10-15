using ServiceStatusBot.Models;
using Newtonsoft.Json;

namespace ServiceStatusBot.Services;

public static class SetupHelper
{
    public static void EnsureConfigAndState()
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
                Messages = new Dictionary<string, ulong>(),
                Statuses = new Dictionary<string, ServiceStatus>()
            };
            File.WriteAllText(statePath, JsonConvert.SerializeObject(defaultState, Formatting.Indented));
        }
    }
}
