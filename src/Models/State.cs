namespace StatusBot.Models;

/// <summary>
///     Persisted application state saved to disk. Contains the single status message reference and last-known statuses.
/// </summary>
public class State
{
    /// <summary>
    ///     The single Discord message ID that displays all services in one embed.
    ///     If 0, no message has been posted yet.
    /// </summary>
    public ulong StatusMessageId { get; set; }

    /// <summary>
    ///     UTC instant when the status message was last updated by the bot.
    /// </summary>
    public DateTime StatusMessageLastUpdatedUtc { get; set; }

    /// <summary>Last-known status objects keyed by service name.</summary>
    public Dictionary<string, ServiceStatus> Statuses { get; set; } = new();

    /// <summary>
    ///     State file format version. Increment when making incompatible changes to the persisted shape.
    /// </summary>
    public string Version { get; set; } = "2";
}