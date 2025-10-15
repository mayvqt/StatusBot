using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ServiceStatusBot.Models;
using Microsoft.AspNetCore.Http;

namespace ServiceStatusBot.Services;

public class ApiHost : BackgroundService
{
    private readonly StatusStore _statusStore;
    private IHost? _apiHost;

    public ApiHost(StatusStore statusStore)
    {
        _statusStore = statusStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        app.MapGet("/api/status", () => _statusStore.Statuses);
        app.MapGet("/api/status/{service}", (string service) =>
        {
            if (_statusStore.Statuses.TryGetValue(service, out var status))
                return Results.Ok(status);
            return Results.NotFound();
        });
        await app.RunAsync(stoppingToken);
    }
}
