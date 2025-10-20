namespace ServiceStatusBot.Models;

/// <summary>
/// Represents the runtime status for a single monitored service.
/// Stores timestamps, uptime calculations and counters used by the monitor.
/// </summary>
public class ServiceStatus
{
    /// <summary>True if the last check observed the service as available.</summary>
    public bool Online { get; set; }

    /// <summary>Instant when the status last changed (flip online/offline).</summary>
    public DateTime LastChange { get; set; }

    /// <summary>Instant of the most recent check.</summary>
    public DateTime LastChecked { get; set; }

    /// <summary>Percentage of time the service has been observed up since <see cref="MonitoringSince"/>.</summary>
    public double UptimePercent { get; set; }

    /// <summary>When monitoring of this service began.</summary>
    public DateTime MonitoringSince { get; set; }

    /// <summary>Cumulative seconds observed 'up' across the monitoring window.</summary>
    public double CumulativeUpSeconds { get; set; }

    /// <summary>Number of checks performed for this service.</summary>
    public int TotalChecks { get; set; }
}
