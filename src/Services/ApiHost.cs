using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ServiceStatusBot.Models;
using Microsoft.AspNetCore.Http;

namespace ServiceStatusBot.Services;

public class ApiHost : BackgroundService
{
    private readonly StatusStore _statusStore;

    public ApiHost(StatusStore statusStore)
    {
        _statusStore = statusStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            app.MapGet("/api/status", () =>
            {
                try
                {
                    return Results.Ok(_statusStore.Statuses);
                }
                catch (Exception ex)
                {
                    ErrorHelper.LogError("Error while handling /api/status", ex);
                    return Results.Problem("Internal server error");
                }
            });

            app.MapGet("/api/status/{service}", (string service) =>
            {
                try
                {
                    if (_statusStore.Statuses.TryGetValue(service, out var status))
                        return Results.Ok(status);
                    return Results.NotFound();
                }
                catch (Exception ex)
                {
                    ErrorHelper.LogError($"Error while handling /api/status/{service}", ex);
                    return Results.Problem("Internal server error");
                }
            });

            await app.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutdown requested
        }
        catch (Exception ex)
        {
            ErrorHelper.LogError("Failed to start API host", ex);
        }
    }
}
