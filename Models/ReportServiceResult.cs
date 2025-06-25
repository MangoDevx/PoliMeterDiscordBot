namespace PoliMeterDiscordBot.Models;

public record ReportServiceResult(string FilePath, List<DatabaseRecords.MessageData> Messages);