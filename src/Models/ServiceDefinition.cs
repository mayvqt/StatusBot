namespace StatusBot.Models;

/// <summary>Monitored service endpoint definition</summary>
public class ServiceDefinition
{
    /// <summary>Service identifier</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Check type (HTTP, TCP, ICMP)</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>HTTP endpoint URL</summary>
    public string? Url { get; set; }

    /// <summary>TCP/ICMP hostname</summary>
    public string? Host { get; set; }

    /// <summary>TCP port number</summary>
    public int? Port { get; set; }
}