using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using ServiceStatusBot.Models;

namespace ServiceStatusBot.Services;

/// <summary>
///     Background service that polls configured services and updates their <see cref="ServiceStatus" />.
///     The monitor records timestamps and accumulates 'up' time so uptime percentages persist across restarts.
/// </summary>
public class StatusMonitor : BackgroundService
{
    // Shared HttpClient for all HTTP checks to avoid socket exhaustion and reduce allocations.
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly ConfigManager _configManager;
    private readonly Persistence _persistence;
    private readonly StatusStore _statusStore;

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
                try
                {
                    if (string.IsNullOrWhiteSpace(service.Name))
                    {
                        ErrorHelper.LogWarning("Skipping service with empty Name in config");
                        continue;
                    }

                    var status = new ServiceStatus();
                    status.LastChecked = DateTime.Now;
                    var online = false;

                    try
                    {
                        var type = service.Type?.ToUpperInvariant() ?? string.Empty;
                        switch (type)
                        {
                            case "HTTP":
                                if (!string.IsNullOrEmpty(service.Url))
                                {
                                    using var response = await _httpClient.GetAsync(service.Url, stoppingToken);
                                    // Only treat 2xx as online
                                    online = (int)response.StatusCode >= 200 && (int)response.StatusCode < 300;
                                }

                                break;
                            case "TCP":
                                if (!string.IsNullOrEmpty(service.Host) && service.Port.HasValue)
                                {
                                    using var tcpClient = new TcpClient();
                                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                                    cts.CancelAfter(3000); // Cancel after timeout
                                    try
                                    {
                                        await tcpClient.ConnectAsync(service.Host, service.Port.Value, cts.Token);
                                        online = tcpClient.Connected;
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        online = false; // Timeout or cancellation
                                    }
                                }

                                break;
                            case "ICMP":
                                if (!string.IsNullOrEmpty(service.Host))
                                {
                                    using var ping = new Ping();
                                    var reply = await ping.SendPingAsync(service.Host, 3000);
                                    online = reply.Status == IPStatus.Success;
                                }

                                break;
                            default:
                                ErrorHelper.LogWarning(
                                    $"Unknown service type '{service.Type}' for service '{service.Name}'");
                                break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // propagate cancellation
                        throw;
                    }
                    catch (Exception ex)
                    {
                        ErrorHelper.LogError($"Error checking service '{service.Name}'", ex);
                        online = false;
                    }

                    // Update status
                    var now = DateTime.Now;
                    if (_statusStore.Statuses.TryGetValue(service.Name, out var prevStatus))
                    {
                        // carry-forward monitoring start and cumulative counters
                        status.MonitoringSince = prevStatus.MonitoringSince;
                        status.CumulativeUpSeconds = prevStatus.CumulativeUpSeconds;
                        status.TotalChecks = prevStatus.TotalChecks + 1;

                        // time since last check -> attribute to previous state
                        var elapsed = (now - prevStatus.LastChecked).TotalSeconds;
                        if (elapsed < 0) elapsed = 0;
                        if (prevStatus.Online) status.CumulativeUpSeconds += elapsed;

                        // update last change timestamp if state flipped
                        status.LastChange = prevStatus.Online != online ? now : prevStatus.LastChange;

                        // compute uptime percent over monitoring window
                        var totalObserved = (now - status.MonitoringSince).TotalSeconds;
                        status.UptimePercent = totalObserved > 0 ? status.CumulativeUpSeconds / totalObserved * 100.0 :
                            online ? 100.0 : 0.0;
                    }
                    else
                    {
                        status.MonitoringSince = now;
                        status.LastChange = now;
                        status.CumulativeUpSeconds = online ? 0.0 : 0.0; // we will add elapsed on next check
                        status.TotalChecks = 1;
                        status.UptimePercent = online ? 100.0 : 0.0;
                    }

                    status.Online = online;
                    status.LastChecked = now;
                    _statusStore.Statuses[service.Name] = status;

                    // Also persist the status into the saved State so SaveState() writes it to disk.
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

            _persistence.SaveState();
            await Task.Delay(_configManager.Config.PollIntervalSeconds * 1000, stoppingToken);
        }
    }
}