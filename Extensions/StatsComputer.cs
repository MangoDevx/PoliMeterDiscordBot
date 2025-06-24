using System.Text.RegularExpressions;
using PoliMeterDiscordBot.Models;

namespace PoliMeterDiscordBot.Extensions;

public static class StatsComputer
{
    public static void FillComputedFields(
        IReadOnlyList<DatabaseRecords.MessageData> msgs,
        DatabaseRecords.UserStat stat,
        DatabaseRecords.UserStat? previous)
    {
        // total messages
        stat.TotalMessages = msgs.Count;

        // peak hour
        stat.PeakActivityHour = msgs
            .GroupBy(m => m.Timestamp.Hour)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? 0;

        // external shares
        stat.ExternalLinkShares = msgs.Count(m => m.Content.Contains("SEP:"));

        // bias shifts: new (from LLM) minus old (from 'previous')
        stat.BiasShiftAl = stat.AuthoritarianLeft
                           - (previous?.AuthoritarianLeft ?? 0m);
        stat.BiasShiftAr = stat.AuthoritarianRight
                           - (previous?.AuthoritarianRight ?? 0m);
        stat.BiasShiftLl = stat.LibertarianLeft
                           - (previous?.LibertarianLeft ?? 0m);
        stat.BiasShiftLr = stat.LibertarianRight
                           - (previous?.LibertarianRight ?? 0m);
    }

    /// <summary>
    /// Computes a ChannelStat for the given guild+channel using only local data.
    /// </summary>
    public static DatabaseRecords.ChannelStat ComputeChannelStat(
        ulong guildId,
        ulong channelId,
        IReadOnlyList<DatabaseRecords.MessageData> allMessages)
    {
        // 1) Filter to this channel's messages, ordered by time
        var msgs = allMessages
            .Where(m => m.GuildId == guildId && m.ChannelId == channelId)
            .OrderBy(m => m.Timestamp)
            .ToList();

        // 2) Total volume
        var totalVol = msgs.Count;

        // 3) Top 3 posters ("userId:count")
        var topPostersCsv = string.Join(",",
            msgs
                .GroupBy(m => m.UserId)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => $"{g.Key}:{g.Count()}"));

        // 4) Average response time between different users
        TimeSpan avgResponse;
        if (msgs.Count < 2)
        {
            avgResponse = TimeSpan.Zero;
        }
        else
        {
            double totalSec = 0;
            var count = 0;
            var prev = msgs[0];
            for (var i = 1; i < msgs.Count; i++)
            {
                var curr = msgs[i];
                if (curr.UserId != prev.UserId)
                {
                    totalSec += (curr.Timestamp - prev.Timestamp).TotalSeconds;
                    count++;
                }

                prev = curr;
            }

            avgResponse = count > 0
                ? TimeSpan.FromSeconds(totalSec / count)
                : TimeSpan.Zero;
        }

        // 5) Peak day/hour
        var peakGroup = msgs
            .GroupBy(m => (m.Timestamp.DayOfWeek, m.Timestamp.Hour))
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        var peakDateTime = peakGroup.HasValue
            ? DateTime.Today
                .AddDays(((int)peakGroup.Value.DayOfWeek - (int)DateTime.Today.DayOfWeek + 7) % 7)
                .AddHours(peakGroup.Value.Hour)
            : DateTime.Today;

        // 6) External content ratio ("SEP:" prefix)
        var externalCount = msgs.Count(m => m.Content.StartsWith("SEP:"));
        var extRatio = totalVol > 0
            ? (decimal)externalCount / totalVol
            : 0m;

        // 7) Build and return
        return new DatabaseRecords.ChannelStat
        (
            GuildId: guildId,
            ChannelId: channelId,
            TotalVolume: totalVol,
            TopPosters: topPostersCsv,
            AvgResponseTime: avgResponse,
            PeakDayHour: peakDateTime,
            ExternalContentRatio: extRatio
        );
    }
}