using System.Collections.Concurrent;
using System.Net;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using StatusBot.Models;

namespace StatusBot.Services;

/// <summary>
///     Background service that posts a single Discord embed showing all service statuses.
///     On startup, searches the channel for an existing status message to prevent duplicates.
///     Persists the message ID so updates are applied to the same message across restarts.
/// </summary>
public class DiscordUpdater : BackgroundService
{
    private readonly ConfigManager _configManager;
    private readonly Persistence _persistence;
    private readonly RateLimiter _rateLimiter;
    private readonly StatusStore _statusStore;

    public DiscordUpdater(StatusStore statusStore, Persistence persistence, ConfigManager configManager,
        RateLimiter rateLimiter)
    {
        _statusStore = statusStore;
        _persistence = persistence;
        _configManager = configManager;
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    ///     Main loop: connect to Discord, find or create a single status message, and update it periodically with all service
    ///     statuses in one embed.
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
                discord?.Dispose();
                return;
            }

            var readyTcs = new TaskCompletionSource();
            discord.Ready += () =>
            {
                readyTcs.SetResult();
                return Task.CompletedTask;
            };
            await readyTcs.Task;

            // Set a friendly rich presence (prefer config.PresenceText if provided)
            try
            {
                var presenceText = _configManager.Config.PresenceText;
                if (string.IsNullOrWhiteSpace(presenceText))
                {
                    var presenceTarget = "services";
                    var firstHttp = _configManager.Config.Services?.FirstOrDefault(s =>
                        string.Equals(s.Type, "HTTP", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(s.Url));
                    if (firstHttp != null && !string.IsNullOrWhiteSpace(firstHttp.Url))
                        try
                        {
                            presenceTarget = new Uri(firstHttp.Url).Host;
                        }
                        catch
                        {
                        }

                    presenceText = $"Monitoring {presenceTarget}";
                }

                await discord.SetGameAsync(presenceText, type: ActivityType.Watching);
            }
            catch (Exception ex)
            {
                ErrorHelper.LogWarning($"Failed to set Discord presence: {ex.Message}");
            }

            // Resolve channel
            var channel = discord.GetChannel(channelId) as SocketTextChannel;
            if (channel == null)
            {
                ErrorHelper.LogError($"Discord channel {channelId} not found. DiscordUpdater cannot continue.",
                    new InvalidOperationException("Channel not found"));
                await discord.StopAsync();
                discord.Dispose();
                return;
            }

            // Discover or create the status message
            IUserMessage? statusMessage = null;
            if (_persistence.State.StatusMessageId != 0)
                try
                {
                    statusMessage = await channel.GetMessageAsync(_persistence.State.StatusMessageId) as IUserMessage;
                    if (statusMessage == null)
                        ErrorHelper.LogWarning(
                            $"Stored message ID {_persistence.State.StatusMessageId} not found in channel; will search recent messages.");
                }
                catch (Exception ex)
                {
                    ErrorHelper.LogWarning(
                        $"Failed to fetch stored message {_persistence.State.StatusMessageId}: {ex.Message}");
                }

            // If we still don't have a message, search recent history for an existing status message from this bot
            if (statusMessage == null)
                try
                {
                    var recentMessages = await channel.GetMessagesAsync(50).FlattenAsync();
                    foreach (var msg in recentMessages)
                        if (msg is IUserMessage userMsg && userMsg.Author.Id == discord.CurrentUser.Id &&
                            userMsg.Embeds.Count > 0)
                        {
                            // Check if this embed looks like our status embed (has "Status Bot" footer or "Status" in title)
                            var embed = userMsg.Embeds.First();
                            if (embed.Footer?.Text?.Contains("Status Bot") == true ||
                                embed.Title?.Contains("Status") == true)
                            {
                                statusMessage = userMsg;
                                _persistence.State.StatusMessageId = userMsg.Id;
                                _persistence.SaveState();
                                ErrorHelper.Log($"Discovered existing status message {userMsg.Id} in channel.");
                                break;
                            }
                        }
                }
                catch (Exception ex)
                {
                    ErrorHelper.LogWarning($"Failed to search channel history: {ex.Message}");
                }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Build a single embed with all services
                    var embed = BuildStatusEmbed(discord, _statusStore.Statuses);

                    // Respect cooldown to avoid excessive API calls
                    var cooldown = TimeSpan.FromSeconds(5);
                    var canUpdate = _persistence.State.StatusMessageLastUpdatedUtc == default ||
                                    DateTime.UtcNow - _persistence.State.StatusMessageLastUpdatedUtc >= cooldown;

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
                        // Create new message with retry logic
                        const int maxRetries = 3;
                        for (var attempt = 1; attempt <= maxRetries; attempt++)
                            try
                            {
                                statusMessage = await channel.SendMessageAsync(embed: embed);
                                _persistence.State.StatusMessageId = statusMessage.Id;
                                _persistence.State.StatusMessageLastUpdatedUtc = DateTime.UtcNow;
                                _persistence.SaveState();
                                ErrorHelper.Log($"Created new status message {statusMessage.Id}.");
                                break;
                            }
                            catch (HttpException httpEx) when (httpEx.HttpCode == HttpStatusCode.TooManyRequests &&
                                                               attempt < maxRetries)
                            {
                                ErrorHelper.LogWarning(
                                    $"Discord rate limit hit on message create (attempt {attempt}/{maxRetries}); retrying after delay.");
                                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), stoppingToken);
                            }
                    }
                    else
                    {
                        // Update existing message with retry logic
                        const int maxRetries = 3;
                        for (var attempt = 1; attempt <= maxRetries; attempt++)
                            try
                            {
                                await statusMessage.ModifyAsync(m => m.Embed = embed);
                                _persistence.State.StatusMessageLastUpdatedUtc = DateTime.UtcNow;
                                _persistence.SaveState();
                                break;
                            }
                            catch (HttpException httpEx) when (httpEx.HttpCode == HttpStatusCode.TooManyRequests &&
                                                               attempt < maxRetries)
                            {
                                ErrorHelper.LogWarning(
                                    $"Discord rate limit hit on message update (attempt {attempt}/{maxRetries}); retrying after delay.");
                                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), stoppingToken);
                            }
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
            finally
            {
                discord?.Dispose();
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
    ///     Build a single embed showing all service statuses.
    /// </summary>
    private Embed BuildStatusEmbed(DiscordSocketClient discord, ConcurrentDictionary<string, ServiceStatus> statuses)
    {
        // greeny-teal accent
        var accent = new Color(20, 160, 120);

        var builder = new EmbedBuilder()
            .WithTitle("📊 Status Dashboard")
            .WithColor(accent)
            .WithFooter(footer => footer.Text = "Status Bot")
            .WithTimestamp(DateTimeOffset.UtcNow);

        // try to use bot avatar as thumbnail
        try
        {
            var avatar = discord?.CurrentUser?.GetAvatarUrl() ?? discord?.CurrentUser?.GetDefaultAvatarUrl();
            if (!string.IsNullOrEmpty(avatar)) builder.WithThumbnailUrl(avatar);
        }
        catch
        {
        }

        if (statuses.Count == 0)
        {
            builder.WithDescription("No services configured.");
            return builder.Build();
        }

        var total = statuses.Count;
        var up = statuses.Count(k => k.Value.Online);
        var down = total - up;
        builder.WithDescription($"**{up}/{total}** services online — {down} offline");

        foreach (var kvp in statuses.OrderBy(s => s.Key))
        {
            var name = kvp.Key;
            var status = kvp.Value;

            var icon = status.Online ? "🟢" : "🔴";
            var statusText = status.Online ? "Online" : "Offline";
            var uptime = $"{status.UptimePercent:F2}%";

            var lastCheckedTimestamp = FormatDiscordTimestamp(status.LastChecked);
            var lastChangeTimestamp = FormatDiscordTimestamp(status.LastChange);

            var fieldValue =
                $"{icon} **{statusText}** · {uptime}\nLast: {lastCheckedTimestamp}\nChanged: {lastChangeTimestamp}";

            // stacked vertically for readability
            // bold the field title for visual hierarchy
            builder.AddField($"**{name}**", fieldValue);
        }

        return builder.Build();
    }

    /// <summary>
    ///     Helper: produce a Discord timestamp token from a DateTime using server local time.
    ///     Treats Unspecified as Local to preserve server timezone behavior.
    /// </summary>
    private string FormatDiscordTimestamp(DateTime dt)
    {
        var kind = dt.Kind == DateTimeKind.Unspecified ? DateTimeKind.Local : dt.Kind;
        var dto = DateTime.SpecifyKind(dt, kind);
        var unix = new DateTimeOffset(dto).ToUnixTimeSeconds();
        return $"<t:{unix}:R>"; // :R = relative time (e.g., "2 minutes ago")
    }
}