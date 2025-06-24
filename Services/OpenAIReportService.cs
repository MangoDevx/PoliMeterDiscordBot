using OpenAI;
using OpenAI.Chat;

namespace PoliMeterDiscordBot.Services;

public class OpenAIReportService(OpenAIClient client)
{
    public async Task<string> AnalyzeStats(string json)
    {
        throw new NotImplementedException();
    }
}