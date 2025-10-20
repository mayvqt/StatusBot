namespace ServiceStatusBot.Models;

public class State
{
    
    public Dictionary<string, MessageReference> MessageMetadata { get; set; } = new();

    public Dictionary<string, ServiceStatus> Statuses { get; set; } = new();
}

public class MessageReference
{
    public ulong Id { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
}
