namespace ServiceStatusBot.Models;

public class Config
{
    public string Token { get; set; } = string.Empty;
    public ulong ChannelId { get; set; }
    public int PollIntervalSeconds { get; set; } = 60;
    public List<ServiceDefinition> Services { get; set; } = new();
}
