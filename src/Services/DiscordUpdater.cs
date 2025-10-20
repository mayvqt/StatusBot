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
                            string DiscordTimestamp(DateTime dt) => $"<t:{new DateTimeOffset(dt).ToUnixTimeSeconds()}:f>";
                            var accent = status.Online ? new Color(0, 180, 170) : new Color(220, 20, 60); // teal when online, red when offline
                            var embed = new EmbedBuilder()
                                .WithTitle($"{name} Status")
                                .WithDescription($"**Status:** {(status.Online ? "🟢 Online" : "🔴 Offline")}\n**Uptime:** {status.UptimePercent:F2}%")
                                .AddField("Last Change", DiscordTimestamp(status.LastChange), true)
                                .AddField("Last Checked", DiscordTimestamp(status.LastChecked), true)
                                .WithColor(accent)
                                .WithFooter(footer => footer.Text = "StatusBot")
                                .Build();

                            // Support migration from legacy Messages -> MessageMetadata
                            ServiceStatusBot.Models.MessageReference? meta = null;
                            if (_persistence.State.MessageMetadata != null && _persistence.State.MessageMetadata.TryGetValue(name, out var existingMeta))
                            {
                                meta = existingMeta;
                            }
                            else if (_persistence.State.Messages != null && _persistence.State.Messages.TryGetValue(name, out var legacyId) && legacyId != 0)
                            {
                                // Try to fetch legacy message; if found migrate into metadata
                                try
                                {
                                    var legacyMsg = await channel.GetMessageAsync(legacyId) as IUserMessage;
                                    if (legacyMsg != null)
                                    {
                                        meta = new ServiceStatusBot.Models.MessageReference { Id = legacyMsg.Id, LastUpdatedUtc = DateTime.UtcNow };
                                        _persistence.State.MessageMetadata[name] = meta;
                                        _persistence.State.Messages.Remove(name);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ErrorHelper.LogWarning($"Could not fetch legacy message {legacyId} for service {name}: {ex.Message}");
                                }
                            }

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

                            if (msg == null)
                            {
                                var newMsg = await channel.SendMessageAsync(embed: embed);
                                var newMeta = new ServiceStatusBot.Models.MessageReference { Id = newMsg.Id, LastUpdatedUtc = DateTime.UtcNow };
                                _persistence.State.MessageMetadata[name] = newMeta;
                            }
                            else
                            {
                                await msg.ModifyAsync(m => m.Embed = embed);
                                // update metadata timestamp
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
