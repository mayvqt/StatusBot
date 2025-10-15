namespace ServiceStatusBot.Models;

public class ServiceStatus
{
    public bool Online { get; set; }
    public DateTime LastChange { get; set; }
    public DateTime LastChecked { get; set; }
    public double UptimePercent { get; set; }
}
