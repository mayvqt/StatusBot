namespace StatusBot.Models;

/// <summary>
///     Defines a service to monitor: name, type and connection details.
///     Supported types include "HTTP", "TCP" and "ICMP".
/// </summary>
public class ServiceDefinition
{
    /// <summary>Unique name for the service (used as the status identifier).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Type of check to perform (e.g., HTTP, TCP, ICMP).</summary>
    public string Type { get; set; } = string.Empty; // HTTP, TCP, ICMP

    /// <summary>URL used for HTTP checks (if Type is HTTP).</summary>
    public string? Url { get; set; }

    /// <summary>Host used for TCP/ICMP checks.</summary>
    public string? Host { get; set; }

    /// <summary>Optional TCP port to use for TCP checks.</summary>
    public int? Port { get; set; }
}