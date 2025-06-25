using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoliMeterDiscordBot.Database;
using PoliMeterDiscordBot.Interfaces;
using PoliMeterDiscordBot.Models;

namespace PoliMeterDiscordBot.Services;

public class WeeklyReportService(
    DatasetBuilderService datasetBuilderService,
    ILogger<WeeklyReportService> logger,
    IDbContextFactory<AppDbContext> contextFactory)
    : IReportService
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    public async Task<ReportServiceResult> GenerateReportAsync(string rootFolder)
    {
        // 1) Determine current week (Sunday → Saturday)
        var today     = DateTime.Today;
        int offset    = (int)today.DayOfWeek;                  // Sunday=0…Saturday=6
        var weekStart = today.AddDays(-offset);
        var weekEnd   = weekStart.AddDays(7);                 // exclusive end
        var weekTag   = $"{weekStart:yyyyMMdd}-{weekEnd.AddDays(-1):yyyyMMdd}";

        // 2) Prepare output directory & file path
        var outDir    = Path.Combine(
            Directory.GetCurrentDirectory(),
            rootFolder,
            weekTag
        );
        Directory.CreateDirectory(outDir);

        var fileName  = $"dataset_{weekTag}.json";
        var filePath  = Path.Combine(outDir, fileName);

        // 3) Load only this week's messages
        await using var db = await contextFactory.CreateDbContextAsync();
        var messages = await db.Set<DatabaseRecords.MessageData>()
            .Where(m => m.Timestamp >= weekStart && m.Timestamp < weekEnd)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        // 4) Build your sampled dataset
        var dataset = datasetBuilderService.BuildDataset(messages);

        // 5) Serialize to JSON
        var json = JsonSerializer.Serialize(dataset, _jsonSerializerOptions);

        // 6) Write JSON to file
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
        logger.LogInformation(
            "Wrote dataset ({Count} msgs) to {Path}",
            dataset.Count, filePath
        );

        // 7) Return the result so the job can send json to the AI
        return new ReportServiceResult(filePath, dataset);
    }
}