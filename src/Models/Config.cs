namespace ServiceStatusBot.Models;

/// <summary>
/// Application configuration loaded from <c>config/config.json</c>.
/// Contains the Discord bot token, target channel, polling interval, and service definitions.
/// </summary>
public class Config
{
    /// <summary>The Discord bot token to use for connecting.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>The Discord channel id where status messages will be posted.</summary>
    public ulong ChannelId { get; set; }

    /// <summary>Optional presence text shown in the bot's activity (overrides auto-detection).</summary>
    public string PresenceText { get; set; } = string.Empty;

    /// <summary>Polling interval (in seconds) used by the status monitor.</summary>
    public int PollIntervalSeconds { get; set; } = 60;

    /// <summary>List of services to monitor.</summary>
    public List<ServiceDefinition> Services { get; set; } = new();
}
