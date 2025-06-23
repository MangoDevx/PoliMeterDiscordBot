using Discord;
using Discord.Interactions;
using PoliMeterDiscordBot.Interfaces;

namespace PoliMeterDiscordBot.Commands;

[RequireUserPermission(GuildPermission.Administrator, Group = "Permission")]
[RequireOwner(Group = "Permission")]
public sealed class RegistrationModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IRegistrationService _regSvc;

    public RegistrationModule(IRegistrationService regSvc)
        => _regSvc = regSvc;

    [SlashCommand("register", "Register this channel for analytics")]
    public async Task RegisterAsync(ITextChannel channelToRegister, ITextChannel reportChannel)
    {
        if (Context.Guild is null)
        {
            await RespondAsync("Can only be used in a guild.", ephemeral: true);
        }
        else
        {
            await _regSvc.RegisterChannelAsync(Context.Guild.Id, channelToRegister.Id, reportChannel.Id);
            await RespondAsync(
                $"🔔 Registered **{channelToRegister.Mention}** for analytics! Weekly reports will post in {reportChannel.Mention}.");
        }
    }
}