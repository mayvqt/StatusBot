namespace StatusBot.Models;

/// <summary>Configuration settings for the status bot</summary>
public class Config
{
    /// <summary>Discord bot auth token</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Target Discord channel for status updates</summary>
    public ulong ChannelId { get; set; }

    /// <summary>Custom bot presence text (empty = auto-detect)</summary>
    public string PresenceText { get; set; } = string.Empty;

    /// <summary>Status check interval in seconds</summary>
    public int PollIntervalSeconds { get; set; } = 60;

    /// <summary>Monitored service endpoints</summary>
    public List<ServiceDefinition> Services { get; set; } = new();
}