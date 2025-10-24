using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ServiceStatusBot.Services;

/// <summary>
///     Lightweight API host that exposes the current status map via a minimal HTTP API.
///     This is intended for local inspection and health checks and runs in the same process
///     as the monitoring services.
/// </summary>
public class ApiHost : BackgroundService
{
    private readonly StatusStore _statusStore;

    public ApiHost(StatusStore statusStore)
    {
        _statusStore = statusStore;
    }

    /// <summary>
    ///     Starts a minimal WebApplication containing two endpoints:
    ///     <c>/api/status</c> and <c>/api/status/{service}</c>.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var builder = WebApplication.CreateBuilder();

            // Respect ASPNETCORE_URLS if provided; otherwise default to 0.0.0.0:4130
            var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            if (string.IsNullOrWhiteSpace(urls))
            {
                // UseSetting("urls") will be picked up by Kestrel on build
                builder.WebHost.UseSetting("urls", "http://0.0.0.0:4130");
                ErrorHelper.Log("ApiHost: no ASPNETCORE_URLS set, defaulting to http://0.0.0.0:4130");
            }

            // Optional CORS: configure allowed origins via env var StatusBot__AllowedOrigins (comma-separated).
            // Use '*' to allow any origin (not recommended for production).
            var allowedOrigins = Environment.GetEnvironmentVariable("StatusBot__AllowedOrigins");
            if (!string.IsNullOrWhiteSpace(allowedOrigins))
            {
                var origins = allowedOrigins.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).ToArray();
                if (origins.Length > 0)
                {
                    builder.Services.AddCors(options =>
                    {
                        options.AddPolicy("StatusBotCors", policy =>
                        {
                            if (origins.Length == 1 && origins[0] == "*")
                                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                            else
                                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
                        });
                    });
                    ErrorHelper.Log($"ApiHost: configured CORS origins: {string.Join(',', origins)}");
                }
            }

            var app = builder.Build();

            // If CORS was configured, enable it
            if (builder.Services.Any(sd => sd.ServiceType == typeof(CorsOptions))) app.UseCors("StatusBotCors");

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