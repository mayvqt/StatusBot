using Microsoft.Extensions.Hosting;
using StatusBot.Models;

namespace StatusBot.Services;

/// <summary>Polls configured services and tracks uptime</summary>
public class StatusMonitor : BackgroundService
{
    private readonly ConfigManager _configManager;
    private readonly StatusStore _statusStore;
    private readonly Persistence _persistence;
    private static readonly HttpClient _httpClient = new HttpClient()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    /// <summary>Create monitor with dependencies</summary>
    public StatusMonitor(ConfigManager configManager, StatusStore statusStore, Persistence persistence)
    {
        _configManager = configManager;
        _statusStore = statusStore;
        _persistence = persistence;
    }

    /// <summary>Run monitoring loop</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var service in _configManager.Config.Services)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(service.Name))
                    {
                        ErrorHelper.LogWarning("Skipping service with empty Name in config");
                        continue;
                    }

                    var status = new ServiceStatus();
                    status.LastChecked = DateTime.Now;
                    bool online = false;

                    try
                    {
                        var type = service.Type?.ToUpperInvariant() ?? string.Empty;
                        switch (type)
                        {
                            case "HTTP":
                                if (!string.IsNullOrEmpty(service.Url))
                                {
                                    using var response = await _httpClient.GetAsync(service.Url, stoppingToken);
                                    online = ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300);
                                }
                                break;
                            case "TCP":
                                if (!string.IsNullOrEmpty(service.Host) && service.Port.HasValue)
                                {
                                    using var tcpClient = new System.Net.Sockets.TcpClient();
                                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                                    cts.CancelAfter(3000);
                                    try
                                    {
                                        await tcpClient.ConnectAsync(service.Host, service.Port.Value, cts.Token);
                                        online = tcpClient.Connected;
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        online = false;
                                    }
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
                            default:
                                ErrorHelper.LogWarning($"Unknown service type '{service.Type}' for service '{service.Name}'");
                                break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        ErrorHelper.LogError($"Error checking service '{service.Name}'", ex);
                        online = false;
                    }

                    var now = DateTime.Now;
                    if (_statusStore.Statuses.TryGetValue(service.Name, out var prevStatus))
                    {
                        status.MonitoringSince = prevStatus.MonitoringSince;
                        status.CumulativeUpSeconds = prevStatus.CumulativeUpSeconds;
                        status.TotalChecks = prevStatus.TotalChecks + 1;

                        var elapsed = (now - prevStatus.LastChecked).TotalSeconds;
                        if (elapsed < 0) elapsed = 0;
                        if (prevStatus.Online)
                        {
                            status.CumulativeUpSeconds += elapsed;
                        }

                        status.LastChange = prevStatus.Online != online ? now : prevStatus.LastChange;

                        var totalObserved = (now - status.MonitoringSince).TotalSeconds;
                        status.UptimePercent = totalObserved > 0 ? (status.CumulativeUpSeconds / totalObserved) * 100.0 : (online ? 100.0 : 0.0);
                    }
                    else
                    {
                        status.MonitoringSince = now;
                        status.LastChange = now;
                        status.CumulativeUpSeconds = online ? 0.0 : 0.0;
                        status.TotalChecks = 1;
                        status.UptimePercent = online ? 100.0 : 0.0;
                    }

                    status.Online = online;
                    status.LastChecked = now;
                    _statusStore.Statuses[service.Name] = status;

                    _persistence.State.Statuses ??= new Dictionary<string, ServiceStatus>();
                    _persistence.State.Statuses[service.Name] = status;
                }
                catch (OperationCanceledException)
                {
                    // Cancellation requested: break out so hosted service can stop promptly.
                    break;
                }
                catch (Exception ex)
                {
                    ErrorHelper.LogError("Unexpected error in StatusMonitor loop", ex);
                }
            }
            _persistence.SaveState();
            await Task.Delay(_configManager.Config.PollIntervalSeconds * 1000, stoppingToken);
        }
    }
}
