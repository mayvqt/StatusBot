using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceStatusBot.Models;
using ServiceStatusBot.Services;

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
