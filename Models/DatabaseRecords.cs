namespace PoliMeterDiscordBot.Models;

public static class DatabaseRecords
{
    public sealed record RegisteredChannel(ulong GuildId, ulong ChannelId, ulong ReportChannelId)
    {
        public int Id { get; init; }
    }

    public sealed record MessageData(
        ulong UserId,
        ulong GuildId,
        ulong ChannelId,
        string Content,
        DateTime Timestamp,
        ulong? ParentMessageId = null)
    {
        public int Id { get; init; }
    }

    public sealed record UserStat(
        ulong GuildId,
        ulong UserId,
        double SentimentScore,
        decimal AuthoritarianLeft,
        decimal AuthoritarianRight,
        decimal LibertarianLeft,
        decimal LibertarianRight)
    {
        public int Id { get; init; }
        public int TotalMessages { get; set; }
        public int PeakActivityHour { get; set; }
        public int ExternalLinkShares { get; set; }
        public decimal BiasShiftAl { get; set; }
        public decimal BiasShiftAr { get; set; }
        public decimal BiasShiftLl { get; set; }
        public decimal BiasShiftLr { get; set; }
    }

    public sealed record ChannelStat(
        ulong GuildId,
        ulong ChannelId,
        int TotalVolume,
        string TopPosters,
        TimeSpan AvgResponseTime,
        DateTime PeakDayHour,
        decimal ExternalContentRatio)
    {
        public int Id { get; init; }
    }
}