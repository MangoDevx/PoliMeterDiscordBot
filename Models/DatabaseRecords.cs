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
        DateTime Timestamp)
    {
        public int Id { get; init; }
    }

    public sealed record UserBias(
        ulong GuildId,
        ulong UserId,
        decimal AuthoritarianLeft,
        decimal AuthoritarianRight,
        decimal LibertarianLeft,
        decimal LibertarianRight)
    {
        public int Id { get; init; }
    }
}