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
    ILogger<WeeklyJobReport> logger,
    IReportService reportService,
    OpenAIReportService aiReportService,
    IOptions<BotSettings> options,
    DiscordSocketClient client,
    IDbContextFactory<AppDbContext> contextFactory) : IJob
{
    private readonly JsonSerializerOptions? _jsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task Execute(IJobExecutionContext _)
    {
        var today = DateTime.Today;
        logger.LogInformation("Weekly report for {Date}", today);

        var dataset = await reportService.GenerateReportAsync("reports");
        var rawJson = JsonSerializer.Serialize(dataset.Messages, _jsonOptions);

        logger.LogInformation("Dataset built: {Count} messages", dataset.Messages.Count);

        if (!options.Value.UseLlm)
        {
            logger.LogInformation("UseLlm=false; skipping LLM call");
            return;
        }

        // --- 2) Call LLM and parse into DTOs.AllGuildStats ---
        DTOs.AllGuildStats allStats;
        try
        {
            var llmJson = await aiReportService.AnalyzeStats(rawJson);
            if (llmJson is null)
            {
                logger.LogError("Failed to get llm response");
                return;
            }

            allStats = JsonSerializer.Deserialize<DTOs.AllGuildStats>(llmJson, _jsonOptions)!
                       ?? throw new Exception("Deserialized AllGuildStats was null");
            logger.LogInformation("LLM returned {G} guilds", allStats.GuildData.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM analysis failed");
            return;
        }

        // --- 3) Persist per-user stats & compute per-channel stats ---
        await using var db = await contextFactory.CreateDbContextAsync();
        var regs = await db.Set<DatabaseRecords.RegisteredChannel>().ToListAsync();

        var allMessages = await db.Set<DatabaseRecords.MessageData>().ToListAsync();
        foreach (var guildStats in allStats.GuildData)
        {
            // 3a) Upsert UserStats
            foreach (var u in guildStats.UserStats)
            {
                // fill computed fields from raw allMessages
                var userMsgs = allMessages
                    .Where(m => m.GuildId == u.GuildId && m.UserId == u.UserId)
                    .ToList();

                var prev = await db.Set<DatabaseRecords.UserStat>()
                    .FindAsync(u.UserId, u.GuildId);

                StatsComputer.FillComputedFields(userMsgs, u, prev);

                if (prev is null)
                    db.Set<DatabaseRecords.UserStat>().Add(u);
                else
                    db.Set<DatabaseRecords.UserStat>().Update(u);
            }

            await db.SaveChangesAsync();

            // 3b) Upsert ChannelStats
            var channelIds = regs
                .Where(r => r.GuildId == guildStats.GuildId)
                .Select(r => r.ChannelId)
                .Distinct();

            foreach (var chId in channelIds)
            {
                var cs = StatsComputer.ComputeChannelStat(
                    guildStats.GuildId,
                    chId,
                    allMessages);

                var exist = await db.Set<DatabaseRecords.ChannelStat>()
                    .FirstOrDefaultAsync(x =>
                        x.GuildId == cs.GuildId &&
                        x.ChannelId == cs.ChannelId);

                if (exist is null) db.Set<DatabaseRecords.ChannelStat>().Add(cs);
                else db.Set<DatabaseRecords.ChannelStat>().Update(cs);
            }

            await db.SaveChangesAsync();

            // --- 4) Send two embeds per guild ---
            var reg = regs.FirstOrDefault(r => r.GuildId == guildStats.GuildId);
            if (reg == null) continue;
            if (client.GetChannel(reg.ReportChannelId) is not IMessageChannel channel)
            {
                logger.LogWarning("Channel {C} for guild {G} not found",
                    reg.ReportChannelId, guildStats.GuildId);
                continue;
            }

            // Guild title
            var guild = client.GetGuild(guildStats.GuildId);
            var title = guild?.Name ?? $"Guild {guildStats.GuildId}";

            // Embed #1: channel stats
            var eb1 = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(Color.DarkBlue)
                .WithFooter($"Week ending {today:yyyy-MM-dd}");

            var chStats = await db.Set<DatabaseRecords.ChannelStat>()
                .Where(c => c.GuildId == guildStats.GuildId)
                .ToListAsync();

            eb1.AddField("Trending Topics",
                string.Join(", ", guildStats.TopTrendingTopics));

            foreach (var cs in chStats)
            {
                eb1.AddField(
                    $"#{cs.ChannelId}",
                    $"Vol: {cs.TotalVolume}, Top: {cs.TopPosters}\n" +
                    $"Avg Resp: {cs.AvgResponseTime:hh\\:mm\\:ss}, Peak: {cs.PeakDayHour:MMM d HH}:00\n" +
                    $"External Content %: {cs.ExternalContentRatio:P1}",
                    inline: false
                );
            }

            // Embed #2: top 20 chatters with full stats
            var topUsers = guildStats.UserStats
                .OrderByDescending(u => u.TotalMessages)
                .Take(20);

            var eb2 = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(Color.DarkGreen)
                .WithFooter($"Week ending {today:yyyy-MM-dd} – Top 20 Chatters");

            foreach (var u in topUsers)
            {
                eb2.AddField(
                    $"{u.UserId}",
                    $"Msgs: {u.TotalMessages}, PeakHr: {u.PeakActivityHour:00}:00, Links: {u.ExternalLinkShares}\n" +
                    $"Sent: {u.SentimentScore:+0.00;-0.00;0.00}\n" +
                    $"AL: {Fmt(u.AuthoritarianLeft, u.BiasShiftAl)}, " +
                    $"AR: {Fmt(u.AuthoritarianRight, u.BiasShiftAr)}\n" +
                    $"LL: {Fmt(u.LibertarianLeft, u.BiasShiftLl)}, " +
                    $"LR: {Fmt(u.LibertarianRight, u.BiasShiftLr)}",
                    inline: false
                );
                continue;

                string Fmt(decimal v, decimal s)
                    => $"{v:F0}% ({(s >= 0 ? "+" : "–")}{Math.Abs(s):F0}%)";
            }

            await channel.SendMessageAsync(embed: eb1.Build());
            await channel.SendMessageAsync(embed: eb2.Build());

            logger.LogInformation(
                "Posted weekly report for guild {G} to channel {C}",
                guildStats.GuildId, reg.ReportChannelId);
        }
    }
}