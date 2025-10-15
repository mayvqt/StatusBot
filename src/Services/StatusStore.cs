using System.Collections.Concurrent;
using ServiceStatusBot.Models;

namespace ServiceStatusBot.Services;

public class StatusStore
{
    public ConcurrentDictionary<string, ServiceStatus> Statuses { get; } = new();
}
