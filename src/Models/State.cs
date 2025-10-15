namespace ServiceStatusBot.Models;

public class State
{
    public Dictionary<string, ulong> Messages { get; set; } = new();
    public Dictionary<string, ServiceStatus> Statuses { get; set; } = new();
}
