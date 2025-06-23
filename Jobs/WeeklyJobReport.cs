using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoliMeterDiscordBot.Database;
using PoliMeterDiscordBot.Interfaces;
using PoliMeterDiscordBot.Models;
using PoliMeterDiscordBot.Services;
using Quartz;

namespace PoliMeterDiscordBot.Jobs;

public sealed class WeeklyJobReport(
    IReportService reportService,
    ILogger<WeeklyJobReport> logger,
    OpenAIReportService aiReportService,
    IServiceProvider serviceProvider,
    IOptions<BotSettings> options,
    IDbContextFactory<AppDbContext> contextFactory) : IJob
{
    private JsonSerializerOptions? _jsonOptions;

    public async Task Execute(IJobExecutionContext context)
    {
        var today = DateTime.Now.Date;
        logger.LogInformation("Weekly report triggered on {Date}", today);

        try
        {
            var reportResult = await reportService.GenerateReportAsync("weeklyreports");
            logger.LogInformation("Weekly report generated at {Path}", reportResult.FilePath);

            if (!options.Value.UseLlm)
            {
                logger.LogInformation("UseLlm=false, skipping LLM analysis");
                return;
            }

            try
            {
                _jsonOptions ??= new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
                var json = await aiReportService.AnalyzeCsvToJsonAsync(reportResult.TextContent);
                var results = JsonSerializer.Deserialize<DatabaseRecords.UserBias[]>(json, _jsonOptions);

                logger.LogInformation("LLM returned {Count} bias entries", results.Length);

                await using var db = await contextFactory.CreateDbContextAsync();

                foreach (var r in results)
                {
                    var existing = await db.UserBiases.FindAsync(r.UserId, r.GuildId);
                    if (existing is null)
                    {
                        db.UserBiases.Add(new DatabaseRecords.UserBias(
                            UserId: r.UserId,
                            GuildId: r.GuildId,
                            AuthoritarianLeft: r.AuthoritarianLeft,
                            AuthoritarianRight: r.AuthoritarianRight,
                            LibertarianLeft: r.LibertarianLeft,
                            LibertarianRight: r.LibertarianRight
                        ));
                    }
                    else
                    {
                        var blended = existing with
                        {
                            AuthoritarianLeft = (existing.AuthoritarianLeft + r.AuthoritarianLeft) / 2,
                            AuthoritarianRight = (existing.AuthoritarianRight + r.AuthoritarianRight) / 2,
                            LibertarianLeft = (existing.LibertarianLeft + r.LibertarianLeft) / 2,
                            LibertarianRight = (existing.LibertarianRight + r.LibertarianRight) / 2
                        };
                        db.UserBiases.Update(blended);
                    }
                }

                await db.SaveChangesAsync();
                logger.LogInformation("UserBias table updated with the latest weekly data");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get weekly analysis");
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate weekly report");
        }
    }
}