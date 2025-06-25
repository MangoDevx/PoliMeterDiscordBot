using Microsoft.EntityFrameworkCore;
using PoliMeterDiscordBot.Database;
using PoliMeterDiscordBot.Interfaces;
using PoliMeterDiscordBot.Models;

namespace PoliMeterDiscordBot.Services;

public sealed class RegistrationService(IDbContextFactory<AppDbContext> contextFactory, IServiceProvider provider)
    : IRegistrationService
{
    public async Task RegisterChannelAsync(ulong guildId, ulong channelId, ulong reportChannelId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        if (!await db.RegisteredChannels.AnyAsync(c => c.GuildId == guildId && c.ChannelId == channelId))
        {
            db.RegisteredChannels.Add(new DatabaseRecords.RegisteredChannel(guildId, channelId, reportChannelId));
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> IsChannelRegisteredAsync(ulong guildId, ulong channelId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();
        return await db.RegisteredChannels.AnyAsync(c => c.GuildId == guildId && c.ChannelId == channelId);
    }

    public async Task<bool> DoesIdenticalMessageExist(ulong guildId, ulong channelId, string content)
    {
        await using var db = await contextFactory.CreateDbContextAsync();
        return !(await db.MessageDatas.AnyAsync(x =>
            x.GuildId == guildId && x.ChannelId == channelId && x.Content == content));
    }
}