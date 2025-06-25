namespace PoliMeterDiscordBot.Interfaces;

public interface IRegistrationService
{
    Task RegisterChannelAsync(ulong guildId, ulong channelId, ulong reportChannelId);
    Task<bool> IsChannelRegisteredAsync(ulong guildId, ulong channelId);
    Task<bool> DoesIdenticalMessageExist(ulong guildId, ulong channelId, string content);
}