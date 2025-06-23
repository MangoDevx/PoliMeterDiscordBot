using Discord.Interactions;

namespace PoliMeterDiscordBot.Commands;

public sealed class GeneralModule : InteractionModuleBase<SocketInteractionContext>
{

    [SlashCommand("ping", "replies with pong!")]
    public async Task PingAsync()
    {
        await RespondAsync("pong!");
    }
}