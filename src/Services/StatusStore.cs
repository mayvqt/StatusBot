using System.Collections.Concurrent;
using StatusBot.Models;

namespace StatusBot.Services;

/// <summary>Thread-safe store for service status tracking</summary>
public class StatusStore
{
    /// <summary>Service statuses by name</summary>
    public ConcurrentDictionary<string, ServiceStatus> Statuses { get; } = new();
}