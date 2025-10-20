using Microsoft.Extensions.Hosting;
using ServiceStatusBot.Models;
using Discord;
using Discord.WebSocket;

namespace ServiceStatusBot.Services;

/// <summary>
/// Background service that ensures per-service status messages are posted/updated to Discord.
/// The service will reuse a single Discord client instance and persist message references
/// so messages are updated rather than recreated across restarts.
/// </summary>
public class DiscordUpdater : BackgroundService
{
    private readonly StatusStore _statusStore;
    private readonly Persistence _persistence;
    private readonly ConfigManager _configManager;
    private readonly RateLimiter _rateLimiter;

    public DiscordUpdater(StatusStore statusStore, Persistence persistence, ConfigManager configManager, RateLimiter rateLimiter)
    {
        _statusStore = statusStore;
        _persistence = persistence;
        _configManager = configManager;
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Main loop: connect to Discord, ensure configured channel exists, and iterate over
    /// monitored services to create or update a message per-service.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var token = _configManager.Config.Token;
            var channelId = _configManager.Config.ChannelId;

            if (string.IsNullOrWhiteSpace(token))
            {
                ErrorHelper.LogWarning("Discord token is empty; DiscordUpdater will not start.");
                return;
            }

            var discord = new DiscordSocketClient();
            try
            {
                await discord.LoginAsync(TokenType.Bot, token);
                await discord.StartAsync();
            }
            catch (Exception ex)
            {
                ErrorHelper.LogError("Failed to initialize Discord client", ex);
                return;
            }

            var readyTcs = new TaskCompletionSource();
            discord.Ready += () => { readyTcs.SetResult(); return Task.CompletedTask; };
            await readyTcs.Task;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var channel = discord.GetChannel(channelId) as SocketTextChannel;
                    if (channel == null)
                    {
                        ErrorHelper.LogWarning($"Discord channel {channelId} not found. Retrying...");
                        await Task.Delay(10000, stoppingToken);
                        continue;
                    }

                    foreach (var kvp in _statusStore.Statuses)
                    {
                        try
                        {
                            var name = kvp.Key;
                            var status = kvp.Value;

                            // Helper: produce a Discord <t:...> token from a DateTime using server local time
                            // Treat Unspecified as Local to preserve server timezone behavior
                            string DiscordTimestamp(DateTime dt)
                            {
                                var kind = dt.Kind == DateTimeKind.Unspecified ? DateTimeKind.Local : dt.Kind;
                                var dto = DateTime.SpecifyKind(dt, kind);
                                var unix = new DateTimeOffset(dto).ToUnixTimeSeconds();
                                return $"<t:{unix}:f>";
                            }

                            var accent = status.Online ? new Color(0, 180, 170) : new Color(220, 20, 60);

                            // Lookup existing message metadata (if any)
                            _persistence.State.MessageMetadata ??= new Dictionary<string, ServiceStatusBot.Models.MessageReference>();
                            _persistence.State.MessageMetadata.TryGetValue(name, out var meta);

                            // Use server local time for any embed-level tokens (Discord will render them in viewer timezone)
                            var now = DateTimeOffset.Now;
                            var unix = now.ToUnixTimeSeconds();
                            var embed = new EmbedBuilder()
                                .WithTitle($"{name} Status")
                                .WithDescription($"**Status:** {(status.Online ? "🟢 Online" : "🔴 Offline")}\n**Uptime:** {status.UptimePercent:F2}%")
                                .AddField("Last Change", DiscordTimestamp(status.LastChange), true)
                                .AddField("Last Checked", DiscordTimestamp(status.LastChecked), true)
                                .WithColor(accent)
                                .WithFooter(footer => footer.Text = "Status Bot")
                                .Build();

                            IUserMessage? msg = null;
                            if (meta != null && meta.Id != 0)
                            {
                                try
                                {
                                    msg = await channel.GetMessageAsync(meta.Id) as IUserMessage;
                                }
                                catch (Exception ex)
                                {
                                    ErrorHelper.LogWarning($"Failed to fetch stored message {meta.Id} for service {name}: {ex.Message}");
                                    msg = null;
                                }
                            }

                            // Respect per-message cooldown to avoid rapid updates and a global rate limiter.
                            // We use a conservative cooldown to reduce API traffic and keep state file writes low.
                            var cooldown = TimeSpan.FromSeconds(5);
                            var canUpdateByTime = meta == null || (DateTime.UtcNow - meta.LastUpdatedUtc) >= cooldown;
                            if (!canUpdateByTime)
                            {
                                // Skip this update; it's too soon since last update
                                continue;
                            }

                            if (!_rateLimiter.TryConsume())
                            {
                                // Rate limiter denied this operation; skip this update
                                ErrorHelper.LogWarning("RateLimiter: throttling Discord updates; skipping an update.");
                                continue;
                            }

                            if (msg == null)
                            {
                                var newMsg = await channel.SendMessageAsync(embed: embed);
                                var newMeta = new ServiceStatusBot.Models.MessageReference { Id = newMsg.Id, LastUpdatedUtc = DateTime.UtcNow };
                                _persistence.State.MessageMetadata[name] = newMeta;
                            }
                            else
                            {
                                await msg.ModifyAsync(m => m.Embed = embed);
                                meta = meta ?? new ServiceStatusBot.Models.MessageReference { Id = msg.Id };
                                meta.LastUpdatedUtc = DateTime.UtcNow;
                                _persistence.State.MessageMetadata[name] = meta;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            ErrorHelper.LogError("Error updating a Discord message for a service", ex);
                        }
                    }
                    _persistence.SaveState();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ErrorHelper.LogError("Unexpected error in DiscordUpdater main loop", ex);
                }

                await Task.Delay(_configManager.Config.PollIntervalSeconds * 1000, stoppingToken);
            }

            try
            {
                await discord.StopAsync();
            }
            catch (Exception ex)
            {
                ErrorHelper.LogError("Error stopping Discord client", ex);
            }
        }
        catch (OperationCanceledException)
        {
            // Hosted service is stopping; exit gracefully
        }
        catch (Exception ex)
        {
            ErrorHelper.LogError("Fatal error in DiscordUpdater", ex);
        }
    }
}
