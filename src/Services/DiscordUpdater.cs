using Microsoft.Extensions.Hosting;
using ServiceStatusBot.Models;
using Discord;
using Discord.WebSocket;

namespace ServiceStatusBot.Services;

public class DiscordUpdater : BackgroundService
{
    private readonly StatusStore _statusStore;
    private readonly Persistence _persistence;
    private readonly ConfigManager _configManager;

    public DiscordUpdater(StatusStore statusStore, Persistence persistence, ConfigManager configManager)
    {
        _statusStore = statusStore;
        _persistence = persistence;
        _configManager = configManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = _configManager.Config.Token;
        var channelId = _configManager.Config.ChannelId;
        var discord = new DiscordSocketClient();
        await discord.LoginAsync(TokenType.Bot, token);
        await discord.StartAsync();

        var readyTcs = new TaskCompletionSource();
        discord.Ready += () => { readyTcs.SetResult(); return Task.CompletedTask; };
        await readyTcs.Task;

        while (!stoppingToken.IsCancellationRequested)
        {
            var channel = discord.GetChannel(channelId) as SocketTextChannel;
            if (channel == null)
            {
                await Task.Delay(10000, stoppingToken);
                continue;
            }

            foreach (var kvp in _statusStore.Statuses)
            {
                var name = kvp.Key;
                var status = kvp.Value;
                string DiscordTimestamp(DateTime dt) => $"<t:{new DateTimeOffset(dt).ToUnixTimeSeconds()}:f>";
                var embed = new EmbedBuilder()
                    .WithTitle($"{name} Status")
                    .WithDescription($"**Status:** {(status.Online ? "🟢 Online" : "🔴 Offline")}\n**Uptime:** {status.UptimePercent:F2}%")
                    .AddField("Last Change", DiscordTimestamp(status.LastChange), true)
                    .AddField("Last Checked", DiscordTimestamp(status.LastChecked), true)
                    .WithColor(new Color(0, 180, 170)) // Teal accent, dark
                    .WithFooter(footer => footer.Text = "StatusBot")
                    .WithCurrentTimestamp()
                    .Build();

                ulong messageId = 0;
                _persistence.State.Messages.TryGetValue(name, out messageId);
                IUserMessage? msg = null;
                if (messageId != 0)
                {
                    msg = await channel.GetMessageAsync(messageId) as IUserMessage;
                }

                if (msg == null)
                {
                    var newMsg = await channel.SendMessageAsync(embed: embed);
                    _persistence.State.Messages[name] = newMsg.Id;
                }
                else
                {
                    await msg.ModifyAsync(m => m.Embed = embed);
                }
            }
            _persistence.SaveState();
            await Task.Delay(_configManager.Config.PollIntervalSeconds * 1000, stoppingToken);
        }
        await discord.StopAsync();
    }
}
