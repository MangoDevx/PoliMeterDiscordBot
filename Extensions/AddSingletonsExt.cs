using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using PoliMeterDiscordBot.Database;
using PoliMeterDiscordBot.Interfaces;
using PoliMeterDiscordBot.Models;
using PoliMeterDiscordBot.Services;

namespace PoliMeterDiscordBot.Extensions;

public static class AddSingletonsExt
{
    public static void RegisterSingletons(IServiceCollection services)
    {
        AddSqlite(services);
        AddNormalSingletons(services);
        AddServiceSingletons(services);
        AddDiscordSingletons(services);
    }

    private static void AddSqlite(IServiceCollection services)
    {
        services.AddDbContextFactory<AppDbContext>(opts =>
            opts.UseSqlite("Data Source=polimeter.db")
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
    }

    private static void AddNormalSingletons(IServiceCollection services)
    {
        services.AddSingleton<ChatClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BotSettings>>();
            return new ChatClient(model: "gpt-3,5-turbo", options.Value.LlmToken);
        });
    }

    private static void AddServiceSingletons(IServiceCollection services)
    {
        services.AddSingleton<IRegistrationService, RegistrationService>();
        services.AddSingleton<IReportService, WeeklyReportService>();
        services.AddSingleton<DatasetBuilderService>();
    }

    private static void AddDiscordSingletons(IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var restConfig = new DiscordRestConfig { LogLevel = LogSeverity.Info };
            return new DiscordRestClient(restConfig);
        });

        services.AddSingleton(_ =>
        {
            var socketConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };

            return new DiscordSocketClient(socketConfig);
        });

        services.AddSingleton(sp =>
        {
            var client = sp.GetRequiredService<DiscordSocketClient>();
            return new InteractionService(client, new InteractionServiceConfig() { });
        });
    }
}