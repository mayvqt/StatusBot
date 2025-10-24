using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using StatusBot.Models;
using StatusBot.Services;

// Application entrypoint and host setup. We configure logging, create the DI container
// and register the background services used by the application.
try
{
    SetupHelper.EnsureConfigAndState();

    // Configure Serilog as the logging provider
    Serilog.Log.Logger = new Serilog.LoggerConfiguration()
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .CreateLogger();

    var builder = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            // Core singletons
            services.AddSingleton<ConfigManager>();
            services.AddSingleton<StatusStore>();
            services.AddSingleton<Persistence>();
            services.AddSingleton<RateLimiter>();

            // Background workers
            services.AddHostedService<StatusMonitor>();
            services.AddHostedService<DiscordUpdater>();
            services.AddHostedService<ApiHost>();
        });

    var host = builder.Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    ErrorHelper.LogError("Fatal unhandled exception in application startup", ex);
    Environment.ExitCode = -1;
}
finally
{
    Serilog.Log.CloseAndFlush();
}
