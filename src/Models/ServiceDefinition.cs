namespace ServiceStatusBot.Models;

public class ServiceDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // HTTP, TCP, ICMP
    public string? Url { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
}
