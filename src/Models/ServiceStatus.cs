namespace ServiceStatusBot.Models;

public class ServiceStatus
{
    public bool Online { get; set; }
    public DateTime LastChange { get; set; }
    public DateTime LastChecked { get; set; }
    public double UptimePercent { get; set; }

    // When we first started monitoring this service
    public DateTime MonitoringSince { get; set; }

    // Cumulative number of seconds the service has been observed up
    public double CumulativeUpSeconds { get; set; }

    // Helpful to know how many checks we've done (not strictly required)
    public int TotalChecks { get; set; }
}
