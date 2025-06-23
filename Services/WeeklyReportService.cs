using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoliMeterDiscordBot.Database;
using PoliMeterDiscordBot.Interfaces;
using PoliMeterDiscordBot.Models;

namespace PoliMeterDiscordBot.Services;

public class WeeklyReportService(
    IServiceProvider provider,
    ILogger<WeeklyReportService> logger,
    IDbContextFactory<AppDbContext> contextFactory)
    : IReportService
{
    public async Task<ReportServiceResult> GenerateReportAsync(string rootFolder)
    {
        // 1) Sunday–Saturday window
        var today = DateTime.Today;
        var offset = (int)today.DayOfWeek;
        var startDate = today.AddDays(-offset);
        var endDate = startDate.AddDays(7);
        var weekTag = $"{startDate:yyyyMMdd}-{startDate.AddDays(6):yyyyMMdd}";

        var outDir = Path.Combine(
            Directory.GetCurrentDirectory(),
            rootFolder,
            weekTag
        );
        Directory.CreateDirectory(outDir);

        var fileName = $"weekly_text_{weekTag}.txt";
        var filePath = Path.Combine(outDir, fileName);

        // 3) Load & order messages
        await using var db = await contextFactory.CreateDbContextAsync();

        var messages = await db.MessageDatas
            .Where(m => m.Timestamp >= startDate && m.Timestamp < endDate)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        // 4) Build the log
        var sb = new StringBuilder();
        foreach (var m in messages)
        {
            var content = m.Content.Replace("|>", "");
            sb.Append($"{m.UserId}-{m.GuildId}-{m.ChannelId} {content} |> ");
        }

        var reportText = sb.ToString();

        await File.WriteAllTextAsync(filePath, reportText, Encoding.UTF8);
        logger.LogInformation(
            "Wrote weekly log with {Count} entries to {Path}",
            messages.Count, filePath
        );

        return new ReportServiceResult(filePath, reportText);
    }
}