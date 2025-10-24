using Microsoft.Extensions.Hosting;
using ServiceStatusBot.Models;
using Discord;
using Discord.WebSocket;

namespace ServiceStatusBot.Services;

/// <summary>
/// Background service that posts a single Discord embed showing all service statuses.
/// On startup, searches the channel for an existing status message to prevent duplicates.
/// Persists the message ID so updates are applied to the same message across restarts.
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
    /// Main loop: connect to Discord, find or create a single status message, and update it periodically with all service statuses in one embed.
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

            // Resolve channel
            var channel = discord.GetChannel(channelId) as SocketTextChannel;
            if (channel == null)
            {
                ErrorHelper.LogError($"Discord channel {channelId} not found. DiscordUpdater cannot continue.", new InvalidOperationException("Channel not found"));
                return;
            }

            // Discover or create the status message
            IUserMessage? statusMessage = null;
            if (_persistence.State.StatusMessageId != 0)
            {
                try
                {
                    statusMessage = await channel.GetMessageAsync(_persistence.State.StatusMessageId) as IUserMessage;
                    if (statusMessage == null)
                    {
                        ErrorHelper.LogWarning($"Stored message ID {_persistence.State.StatusMessageId} not found in channel; will search recent messages.");
                    }
                }
                catch (Exception ex)
                {
                    ErrorHelper.LogWarning($"Failed to fetch stored message {_persistence.State.StatusMessageId}: {ex.Message}");
                }
            }

            // If we still don't have a message, search recent history for an existing status message from this bot
            if (statusMessage == null)
            {
                try
                {
                    var recentMessages = await channel.GetMessagesAsync(50).FlattenAsync();
                    foreach (var msg in recentMessages)
                    {
                        if (msg is IUserMessage userMsg && userMsg.Author.Id == discord.CurrentUser.Id && userMsg.Embeds.Count > 0)
                        {
                            // Check if this embed looks like our status embed (has "Status Bot" footer or "Status" in title)
                            var embed = userMsg.Embeds.First();
                            if (embed.Footer?.Text?.Contains("Status Bot") == true || embed.Title?.Contains("Status") == true)
                            {
                                statusMessage = userMsg;
                                _persistence.State.StatusMessageId = userMsg.Id;
                                _persistence.SaveState();
                                ErrorHelper.Log($"Discovered existing status message {userMsg.Id} in channel.");
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorHelper.LogWarning($"Failed to search channel history: {ex.Message}");
                }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Build a single embed with all services
                    var embed = BuildStatusEmbed(_statusStore.Statuses);

                    // Respect cooldown to avoid excessive API calls
                    var cooldown = TimeSpan.FromSeconds(5);
                    var canUpdate = (_persistence.State.StatusMessageLastUpdatedUtc == default) ||
                                   (DateTime.UtcNow - _persistence.State.StatusMessageLastUpdatedUtc) >= cooldown;

                    if (!canUpdate)
                    {
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    if (!_rateLimiter.TryConsume())
                    {
                        ErrorHelper.LogWarning("RateLimiter: throttling Discord updates; skipping an update.");
                        await Task.Delay(1000, stoppingToken);
                        continue;
                    }

                    if (statusMessage == null)
                    {
                        // Create new message
                        statusMessage = await channel.SendMessageAsync(embed: embed);
                        _persistence.State.StatusMessageId = statusMessage.Id;
                        _persistence.State.StatusMessageLastUpdatedUtc = DateTime.UtcNow;
                        _persistence.SaveState();
                        ErrorHelper.Log($"Created new status message {statusMessage.Id}.");
                    }
                    else
                    {
                        // Update existing message
                        await statusMessage.ModifyAsync(m => m.Embed = embed);
                        _persistence.State.StatusMessageLastUpdatedUtc = DateTime.UtcNow;
                        _persistence.SaveState();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ErrorHelper.LogError("Error updating Discord status message", ex);
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

    /// <summary>
    /// Build a single embed showing all service statuses.
    /// </summary>
    private Embed BuildStatusEmbed(System.Collections.Concurrent.ConcurrentDictionary<string, ServiceStatus> statuses)
    {
        var builder = new EmbedBuilder()
            .WithTitle("📊 Service Status Dashboard")
            .WithColor(new Color(0, 120, 215))
            .WithFooter(footer => footer.Text = "Status Bot")
            .WithTimestamp(DateTimeOffset.Now);

        if (statuses.Count == 0)
        {
            builder.WithDescription("No services configured.");
            return builder.Build();
        }

        foreach (var kvp in statuses.OrderBy(s => s.Key))
        {
            var name = kvp.Key;
            var status = kvp.Value;

            var icon = status.Online ? "🟢" : "🔴";
            var statusText = status.Online ? "Online" : "Offline";
            var uptime = $"{status.UptimePercent:F2}%";

            var lastCheckedTimestamp = FormatDiscordTimestamp(status.LastChecked);
            var lastChangeTimestamp = FormatDiscordTimestamp(status.LastChange);

            var fieldValue = $"{icon} **{statusText}** | Uptime: {uptime}\nLast Check: {lastCheckedTimestamp} | Last Change: {lastChangeTimestamp}";

            builder.AddField(name, fieldValue, inline: false);
        }

        return builder.Build();
    }

    /// <summary>
    /// Helper: produce a Discord timestamp token from a DateTime using server local time.
    /// Treats Unspecified as Local to preserve server timezone behavior.
    /// </summary>
    private string FormatDiscordTimestamp(DateTime dt)
    {
        var kind = dt.Kind == DateTimeKind.Unspecified ? DateTimeKind.Local : dt.Kind;
        var dto = DateTime.SpecifyKind(dt, kind);
        var unix = new DateTimeOffset(dto).ToUnixTimeSeconds();
        return $"<t:{unix}:R>"; // :R = relative time (e.g., "2 minutes ago")
    }
}
