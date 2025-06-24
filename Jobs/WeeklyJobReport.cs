using System.Text.Json;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoliMeterDiscordBot.Database;
using PoliMeterDiscordBot.Extensions;
using PoliMeterDiscordBot.Interfaces;
using PoliMeterDiscordBot.Models;
using PoliMeterDiscordBot.Services;
using Quartz;

namespace PoliMeterDiscordBot.Jobs;

public sealed class WeeklyJobReport(
    IReportService reportService,
    ILogger<WeeklyJobReport> logger,
    OpenAIReportService aiReportService,
    IServiceProvider serviceProvider,
    IOptions<BotSettings> options,
    DiscordSocketClient client,
    IDbContextFactory<AppDbContext> contextFactory) : IJob
{
    private JsonSerializerOptions? _jsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task Execute(IJobExecutionContext _)
    {
        var today = DateTime.Today;
        logger.LogInformation("Weekly report triggered for {Date}", today);
        
        var rawJson = string.Empty;
        try
        {
            var result = await reportService.GenerateReportAsync("weeklyreports");
            rawJson = result.TextContent;
            if (string.IsNullOrWhiteSpace(rawJson))
                throw new Exception("Empty JSON");
            logger.LogInformation("Raw JSON length: {Len}", rawJson.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate weekly JSON report");
            return;
        }

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            logger.LogError("Failed to generate weekly JSON report - Json null");
            return;
        }

        if (!options.Value.UseLlm)
        {
            logger.LogInformation("UseLlm=false; skipping LLM analysis");
            return;
        }

        // 2) LLM → per-user stats
        DTOs.AllGuildStats? allStats = null;
        try
        {
            var llmJson = await aiReportService.AnalyzeStats(rawJson);
            allStats = JsonSerializer.Deserialize<DTOs.AllGuildStats>(llmJson, _jsonOptions)!;
            logger.LogInformation("LLM returned stats for {GuildCount} guilds",
                allStats.GuildData.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM analysis failed");
            return;
        }

        if (allStats is null)
        {
            logger.LogError("Failed to analyze weekly report. AllStats is null");
            return;
        }

        // 3) Persist per-user stats
        await using var db = await contextFactory.CreateDbContextAsync();
        var regs = await db.Set<DatabaseRecords.RegisteredChannel>().ToListAsync();

        foreach (var guildStats in allStats.GuildData)
        {
            // upsert UserStat
            foreach (var u in guildStats.UserStats)
            {
                var existing = await db.Set<DatabaseRecords.UserStat>()
                    .FindAsync(u.UserId, u.GuildId);
                if (existing is null)
                    db.Set<DatabaseRecords.UserStat>().Add(u);
                else
                    db.Set<DatabaseRecords.UserStat>().Update(u);
            }

            await db.SaveChangesAsync();

            // 5) Post two embeds per guild
            // find report channel
            var reg = regs.FirstOrDefault(r => r.GuildId == guildStats.GuildId);
            if (reg is null) continue;

            if (client.GetChannel(reg.ReportChannelId) is not IMessageChannel channel)
            {
                logger.LogWarning(
                    "Channel {Chan} for guild {Guild} not found",
                    reg.ReportChannelId, guildStats.GuildId);
                continue;
            }

            // lookup guild name
            var guild = client.GetGuild(guildStats.GuildId);
            var title = guild?.Name ?? $"Guild {guildStats.GuildId}";

            // EMBED 1: Channel stats
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(Color.DarkBlue)
                .WithFooter($"Week ending {today:yyyy-MM-dd}");

            embed.AddField("Guild Trending Topics: ", guildStats.TopTrendingTopics);

            var chStats = await db.Set<DatabaseRecords.ChannelStat>()
                .Where(c => c.GuildId == guildStats.GuildId)
                .ToListAsync();

            foreach (var cs in chStats)
            {
                embed.AddField(
                    $"#{cs.ChannelId}",
                    $"Vol: {cs.TotalVolume}, Top: {cs.TopPosters}\n" +
                    $"Avg Resp: {cs.AvgResponseTime:hh\\:mm\\:ss}, Peak: {cs.PeakDayHour:MMM d HH:00}\n" +
                    $"External Content %: {cs.ExternalContentRatio:P1}\n",
                    inline: false
                );
            }

            // EMBED 2: Top 20 chatters
            var topUsers = guildStats.UserStats
                .OrderByDescending(u => u.TotalMessages)
                .Take(20);

            var eb2 = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(Color.DarkGreen)
                .WithFooter($"Week ending {today:yyyy-MM-dd} – Top 20 Chatters");

            foreach (var u in topUsers)
            {
                string FormatBias(decimal value, decimal shift)
                    => $"{value:F0}% ({(shift >= 0 ? "+" : "–")}{Math.Abs(shift):F0}%)";

                eb2.AddField(
                    $"{u.UserId}",
                    $"Msgs: {u.TotalMessages}, PeakHr: {u.PeakActivityHour:00}:00, Links: {u.ExternalLinkShares}\n" +
                    $"Sent: {u.SentimentScore:+0.00;-0.00;0.00}\n" +
                    $"AL: {FormatBias(u.AuthoritarianLeft, u.BiasShiftAl)}, " +
                    $"AR: {FormatBias(u.AuthoritarianRight, u.BiasShiftAr)}\n" +
                    $"LL: {FormatBias(u.LibertarianLeft, u.BiasShiftLl)}, " +
                    $"LR: {FormatBias(u.LibertarianRight, u.BiasShiftLr)}",
                    inline: false
                );
            }
            
            await channel.SendMessageAsync(embed: embed.Build());
            await channel.SendMessageAsync(embed: eb2.Build());

            logger.LogInformation(
                "Posted weekly report embeds to guild {Guild} channel {Chan}",
                guildStats.GuildId, reg.ReportChannelId);
        }
    }
}