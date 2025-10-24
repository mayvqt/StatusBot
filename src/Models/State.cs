namespace StatusBot.Models;

/// <summary>Persisted application state and status data</summary>
public class State
{
    /// <summary>Discord status message ID (0 if not posted)</summary>
    public ulong StatusMessageId { get; set; }

    /// <summary>Last message update timestamp (UTC)</summary>
    public DateTime StatusMessageLastUpdatedUtc { get; set; }

    /// <summary>Current service statuses</summary>
    public Dictionary<string, ServiceStatus> Statuses { get; set; } = new();

    /// <summary>State schema version</summary>
    public string Version { get; set; } = "2";
}