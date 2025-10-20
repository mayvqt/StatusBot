using System.Collections.Concurrent;
using ServiceStatusBot.Models;

namespace ServiceStatusBot.Services;

/// <summary>
/// In-memory store of the latest observed <see cref="ServiceStatus"/> for each service name.
/// Uses a concurrent dictionary to support safe access from background services.
/// </summary>
public class StatusStore
{
    /// <summary>Latest statuses keyed by service name.</summary>
    public ConcurrentDictionary<string, ServiceStatus> Statuses { get; } = new();
}
