using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoliMeterDiscordBot.Database;
using PoliMeterDiscordBot.Interfaces;
using PoliMeterDiscordBot.Models;

namespace PoliMeterDiscordBot.Services;

public class BotHostService(
    DiscordSocketClient client,
    InteractionService interactions,
    IServiceProvider services,
    IOptions<BotSettings> opts,
    ILogger<BotHostService> logger,
    IRegistrationService registrationService,
    IDbContextFactory<AppDbContext> dbContextFactory)
    : IHostedService
{
    private readonly BotSettings _settings = opts.Value;
    private readonly ILogger _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        client.Log += LogAsync;
        interactions.Log += LogAsync;

        await interactions.AddModulesAsync(typeof(Program).Assembly, services);
        RegisterDiscordClientEvents(cancellationToken);

        await client.LoginAsync(TokenType.Bot, _settings.Token);
        await client.StartAsync();
    }

    private void RegisterDiscordClientEvents(CancellationToken ct)
    {
        client.InteractionCreated += async interaction =>
        {
            var ctx = new SocketInteractionContext(client, interaction);
            await interactions.ExecuteCommandAsync(ctx, services);
        };

        client.MessageReceived += HandleIncomingMessage;

        client.Ready += async () =>
        {
            await interactions.RegisterCommandsGloballyAsync();
            _logger.LogInformation("✅ Slash commands registered globally");
        };
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.LogoutAsync();
        await client.StopAsync();
    }

    private async Task HandleIncomingMessage(SocketMessage message)
    {
        if (message.Author.IsBot)
            return;

        if (message.Channel is not SocketTextChannel ch)
            return;

        await using var db = await dbContextFactory.CreateDbContextAsync();

        if (!(await registrationService.IsChannelRegisteredAsync(ch.Guild.Id, ch.Id)))
            return;
        
        if (string.IsNullOrEmpty(message.Content))
            return;

        var fullText = message.Content ?? string.Empty;
        var tokens = message.Content?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens != null)
        {
            var urls = tokens.Where(t => Uri.TryCreate(t, UriKind.Absolute, out _)).ToList();
            fullText = urls.Aggregate(fullText, (current, urlToken) => current.Replace(urlToken, string.Empty));
        }

        foreach (var embed in message.Embeds)
        {
            var header = embed.Title ?? embed.Author?.Name;
            if (string.IsNullOrWhiteSpace(header) || string.IsNullOrWhiteSpace(embed.Description))
                continue;

            if (fullText.Contains(embed.Url))
                fullText = fullText.Replace(embed.Url, string.Empty);

            fullText += $"SEP: {header}->{embed.Description}\n";
        }

        db.MessageDatas.Add(new DatabaseRecords.MessageData(
            UserId: message.Author.Id,
            GuildId: ch.Guild.Id,
            ChannelId: ch.Id,
            Content: fullText.Trim(),
            Timestamp: message.Timestamp.UtcDateTime
        ));

        await db.SaveChangesAsync();
    }

    private Task LogAsync(LogMessage msg)
    {
        _logger.LogInformation(msg.ToString());
        return Task.CompletedTask;
    }
}