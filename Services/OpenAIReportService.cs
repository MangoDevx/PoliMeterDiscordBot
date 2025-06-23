using OpenAI;
using OpenAI.Chat;

namespace PoliMeterDiscordBot.Services;

public class OpenAIReportService(OpenAIClient client)
{
    public async Task<string> AnalyzeCsvToJsonAsync(string csvText)
    {
        throw new NotImplementedException();
    }
}