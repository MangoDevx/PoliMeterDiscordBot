using PoliMeterDiscordBot.Models;

namespace PoliMeterDiscordBot.Interfaces;

public interface IReportService
{
    Task<ReportServiceResult> GenerateReportAsync(string rootFolder);
}