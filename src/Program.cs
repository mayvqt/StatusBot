using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using ServiceStatusBot.Models;
using ServiceStatusBot.Services;

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
            services.AddSingleton<ConfigManager>();
            services.AddSingleton<StatusStore>();
            services.AddSingleton<Persistence>();
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
