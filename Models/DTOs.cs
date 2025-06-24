namespace PoliMeterDiscordBot.Models;

public static class DTOs
{
    public sealed record GuildStats(ulong GuildId, DatabaseRecords.UserStat[] UserStats, string[] TopTrendingTopics);

    public sealed record AllGuildStats(GuildStats[] GuildData);
}