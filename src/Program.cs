using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStatusBot.Models;
using ServiceStatusBot.Services;

try
{
    SetupHelper.EnsureConfigAndState();

    var builder = Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            services.AddSingleton<ConfigManager>();
            services.AddSingleton<StatusStore>();
            services.AddSingleton<Persistence>();
            services.AddHostedService<StatusMonitor>();
            services.AddHostedService<DiscordUpdater>();
            services.AddHostedService<ApiHost>();
        });

    await builder.Build().RunAsync();
}
catch (Exception ex)
{
    ErrorHelper.LogError("Fatal unhandled exception in application startup", ex);
    // In top-level code we can't return an exit code directly from async main; ensure process exits non-zero
    Environment.ExitCode = -1;
}
