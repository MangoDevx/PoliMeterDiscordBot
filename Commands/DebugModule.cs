using System.Text;
using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using PoliMeterDiscordBot.Database;

namespace PoliMeterDiscordBot.Commands;

[RequireOwner]
public sealed class DebugModule(IDbContextFactory<AppDbContext> contextFactory)
    : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("dumpdb", "Debug: dump the DB contents into an embed (owner only)")]
    public async Task DumpDbAsync()
    {
        await DeferAsync(ephemeral: true);

        await using var db = await contextFactory.CreateDbContextAsync();

        var regs = await db.RegisteredChannels.ToListAsync();
        var biases = await db.UserStats.ToListAsync();
        var msgCnt = await db.MessageDatas.CountAsync();

        var last2 = await db.MessageDatas
            .AsNoTracking()
            .OrderByDescending(m => m.Timestamp)
            .Take(2)
            .ToListAsync();

        var regsText = regs.Any()
            ? string.Join("\n", regs.Select(r => $"{r.GuildId}/{r.ChannelId} → report:{r.ReportChannelId}"))
            : "*<none>*";

        var biasText = biases.Any()
            ? string.Join("\n", biases.Select(b =>
                $"{b.GuildId}/{b.UserId}: AL={b.AuthoritarianLeft:F1}% AR={b.AuthoritarianRight:F1}% LL={b.LibertarianLeft:F1}% LR={b.LibertarianRight:F1}%"))
            : "*<none>*";

        var last2Text = last2.Any()
            ? string.Join("\n", last2.Select(m =>
                $"[{m.Timestamp:O}] {m.GuildId}/{m.ChannelId}/{m.UserId}: {m.Content}"))
            : "*<no messages>*";

        void AddFieldChunks(EmbedBuilder eb, string name, string content)
        {
            const int max = 1024;
            if (content.Length <= max)
            {
                eb.AddField(name, $"```yaml\n{content}\n```");
            }
            else
            {
                int part = 1;
                foreach (var chunk in ChunkString(content, max))
                    eb.AddField($"{name} (part {part++})", $"```yaml\n{chunk}\n```");
            }
        }

        var eb = new EmbedBuilder()
            .WithTitle("🛠️ Database Dump")
            .WithColor(Color.DarkBlue)
            .WithFooter($"Requested by {Context.User.Username} • {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
            .WithDescription($"• RegisteredChannels: {regs.Count} rows\n" +
                             $"• UserBias: {biases.Count} rows\n" +
                             $"• MessageData total count: {msgCnt}\n" +
                             $"• Last 2 messages:");

        AddFieldChunks(eb, "RegisteredChannels", regsText);
        AddFieldChunks(eb, "UserBiases", biasText);
        AddFieldChunks(eb, "Last 2 Messages", last2Text);

        await FollowupAsync(embed: eb.Build());
    }

    private static IEnumerable<string> ChunkString(string text, int maxSize)
    {
        var lines = text.Split('\n');
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            if (sb.Length + line.Length + 1 > maxSize)
            {
                yield return sb.ToString().TrimEnd('\n');
                sb.Clear();
            }

            sb.AppendLine(line);
        }

        if (sb.Length > 0)
            yield return sb.ToString().TrimEnd('\n');
    }
}