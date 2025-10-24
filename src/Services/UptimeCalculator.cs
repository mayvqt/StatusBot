using StatusBot.Models;

namespace StatusBot.Services;

/// <summary>Helper for calculating service uptime metrics</summary>
public static class UptimeCalculator
{
    /// <summary>Update uptime metrics for a service</summary>
    /// <param name="prev">Previous status</param>
    /// <param name="current">Current status to update</param>
    /// <param name="online">Current online state</param>
    /// <param name="now">Current timestamp</param>
    public static void UpdateUptime(ServiceStatus prev, ServiceStatus current, bool online, DateTime now)
    {
        if (prev == null) throw new ArgumentNullException(nameof(prev));
        if (current == null) throw new ArgumentNullException(nameof(current));

        current.MonitoringSince = prev.MonitoringSince == default ? now : prev.MonitoringSince;
        current.CumulativeUpSeconds = prev.CumulativeUpSeconds;
        current.TotalChecks = prev.TotalChecks + 1;

        var elapsed = (now - prev.LastChecked).TotalSeconds;
        if (elapsed < 0) elapsed = 0;
        if (prev.Online) current.CumulativeUpSeconds += elapsed;

        current.LastChange = prev.Online != online ? now : prev.LastChange;
        current.Online = online;
        current.LastChecked = now;

        var totalObserved = (now - current.MonitoringSince).TotalSeconds;
        current.UptimePercent = totalObserved > 0 ? current.CumulativeUpSeconds / totalObserved * 100.0 :
            online ? 100.0 : 0.0;
    }
}