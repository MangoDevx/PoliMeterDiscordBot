namespace PoliMeterDiscordBot.Interfaces;

public interface IRegistrationService
{
    Task RegisterChannelAsync(ulong guildId, ulong channelId, ulong reportChannelId);
    Task<bool> IsChannelRegisteredAsync(ulong guildId, ulong channelId);
}