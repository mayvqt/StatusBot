namespace ServiceStatusBot.Models;

/// <summary>
/// Persisted application state saved to disk. Contains message references and last-known statuses.
/// </summary>
public class State
{
    /// <summary>Per-service Discord message metadata (message id and last updated instant).</summary>
    public Dictionary<string, MessageReference> MessageMetadata { get; set; } = new();

    /// <summary>Last-known status objects keyed by service name.</summary>
    public Dictionary<string, ServiceStatus> Statuses { get; set; } = new();

    /// <summary>
    /// State file format version. Increment when making incompatible changes to the persisted shape.
    /// </summary>
    public string Version { get; set; } = "1";
}

/// <summary>
/// Small reference record used to persist the Discord message id and when it was last updated.
/// </summary>
public class MessageReference
{
    /// <summary>Discord message id.</summary>
    public ulong Id { get; set; }

    /// <summary>UTC instant when the message was last updated by the bot.</summary>
    public DateTime LastUpdatedUtc { get; set; }
}
