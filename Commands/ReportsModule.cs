using Discord;
using Discord.Interactions;
using PoliMeterDiscordBot.Interfaces;

namespace PoliMeterDiscordBot.Commands;

[Discord.Commands.RequireUserPermission(GuildPermission.Administrator, Group = "Permission")]
[Discord.Commands.RequireOwner(Group = "Permission")]
public sealed class ReportsModule(IReportService reportService) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("forcereport", "Force-generate the weekly report into forcedreports/")]
    public async Task ForceReportAsync()
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var fp = await reportService.GenerateReportAsync("forcedreports");
            await FollowupAsync(
                $"✅ Forced reports for the past week  written to `{fp.FilePath}`",
                ephemeral: true
            );
        }
        catch (Exception ex)
        {
            await FollowupAsync(
                $"❌ Failed to generate forced report: {ex.Message}",
                ephemeral: true
            );
        }
    }
}