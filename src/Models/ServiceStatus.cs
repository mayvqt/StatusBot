namespace StatusBot.Models;

/// <summary>Current status and metrics for a monitored service</summary>
public class ServiceStatus
{
    /// <summary>Service is currently available</summary>
    public bool Online { get; set; }

    /// <summary>Last state change timestamp</summary>
    public DateTime LastChange { get; set; }

    /// <summary>Last check timestamp</summary>
    public DateTime LastChecked { get; set; }

    /// <summary>Uptime percentage since monitoring began</summary>
    public double UptimePercent { get; set; }

    /// <summary>Monitoring start timestamp</summary>
    public DateTime MonitoringSince { get; set; }

    /// <summary>Total seconds service was up</summary>
    public double CumulativeUpSeconds { get; set; }

    /// <summary>Total status checks performed</summary>
    public int TotalChecks { get; set; }
}