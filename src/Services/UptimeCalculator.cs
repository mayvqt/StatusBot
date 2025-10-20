using ServiceStatusBot.Models;

namespace ServiceStatusBot.Services;

/// <summary>
/// Helper that encapsulates uptime math used by <see cref="StatusMonitor"/>.
/// The calculator updates cumulative up-seconds, uptime percentage and timestamps
/// based on a previous status and the current observed online flag.
/// </summary>
public static class UptimeCalculator
{
    /// <summary>
    /// Update cumulative up seconds and uptime percent given previous status and current online flag and now.
    /// </summary>
    /// <param name="prev">Previous persisted status (must not be null).</param>
    /// <param name="current">Current status object to populate (must not be null).</param>
    /// <param name="online">Whether the service is observed online on this check.</param>
    /// <param name="now">The current timestamp used for calculations.</param>
    public static void UpdateUptime(ServiceStatus prev, ServiceStatus current, bool online, DateTime now)
    {
        if (prev == null) throw new ArgumentNullException(nameof(prev));
        if (current == null) throw new ArgumentNullException(nameof(current));

        current.MonitoringSince = prev.MonitoringSince == default ? now : prev.MonitoringSince;
        current.CumulativeUpSeconds = prev.CumulativeUpSeconds;
        current.TotalChecks = prev.TotalChecks + 1;

        var elapsed = (now - prev.LastChecked).TotalSeconds;
        if (elapsed < 0) elapsed = 0;
        if (prev.Online)
        {
            current.CumulativeUpSeconds += elapsed;
        }

        current.LastChange = prev.Online != online ? now : prev.LastChange;
        current.Online = online;
        current.LastChecked = now;

        var totalObserved = (now - current.MonitoringSince).TotalSeconds;
        current.UptimePercent = totalObserved > 0 ? (current.CumulativeUpSeconds / totalObserved) * 100.0 : (online ? 100.0 : 0.0);
    }
}
