using Microsoft.Extensions.Hosting;
using ServiceStatusBot.Models;

namespace ServiceStatusBot.Services;

public class StatusMonitor : BackgroundService
{
    private readonly ConfigManager _configManager;
    private readonly StatusStore _statusStore;
    private readonly Persistence _persistence;

    public StatusMonitor(ConfigManager configManager, StatusStore statusStore, Persistence persistence)
    {
        _configManager = configManager;
        _statusStore = statusStore;
        _persistence = persistence;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var service in _configManager.Config.Services)
            {
                var status = new ServiceStatus();
                status.LastChecked = DateTime.UtcNow;
                bool online = false;
                try
                {
                    switch (service.Type.ToUpperInvariant())
                    {
                        case "HTTP":
                            if (!string.IsNullOrEmpty(service.Url))
                            {
                                using var httpClient = new HttpClient();
                                var response = await httpClient.GetAsync(service.Url, stoppingToken);
                                // Only treat 2xx as online
                                online = ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300);
                            }
                            break;
                        case "TCP":
                            if (!string.IsNullOrEmpty(service.Host) && service.Port.HasValue)
                            {
                                using var tcpClient = new System.Net.Sockets.TcpClient();
                                var connectTask = tcpClient.ConnectAsync(service.Host, service.Port.Value);
                                var completed = await Task.WhenAny(connectTask, Task.Delay(3000, stoppingToken));
                                online = connectTask.IsCompletedSuccessfully && tcpClient.Connected;
                            }
                            break;
                        case "ICMP":
                            if (!string.IsNullOrEmpty(service.Host))
                            {
                                using var ping = new System.Net.NetworkInformation.Ping();
                                var reply = await ping.SendPingAsync(service.Host, 3000);
                                online = reply.Status == System.Net.NetworkInformation.IPStatus.Success;
                            }
                            break;
                    }
                }
                catch
                {
                    online = false;
                }

                // Update status
                if (_statusStore.Statuses.TryGetValue(service.Name, out var prevStatus))
                {
                    status.LastChange = prevStatus.Online != online ? DateTime.UtcNow : prevStatus.LastChange;
                    status.UptimePercent = prevStatus.UptimePercent; // TODO: Calculate uptime
                }
                else
                {
                    status.LastChange = DateTime.UtcNow;
                    status.UptimePercent = online ? 100.0 : 0.0;
                }
                status.Online = online;
                _statusStore.Statuses[service.Name] = status;
            }
            _persistence.SaveState();
            await Task.Delay(_configManager.Config.PollIntervalSeconds * 1000, stoppingToken);
        }
    }
}
