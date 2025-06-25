using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace PoliMeterDiscordBot.Services;

public class OpenAIReportService(ChatClient client, ILogger<OpenAIReportService> logger)
{
    private const string _systemPrompt =
        "You are an expert data analyst. " +
        "Given a JSON array of MessageData records, calculate all the analytics required, " +
        "and produce **ONLY** the JSON matching the AllGuildStats schema:\n" +
        "{\n" +
        "  \"GuildData\": [\n" +
        "    {\n" +
        "      \"GuildId\": <ulong>,\n" +
        "      \"UserStats\": [ /* array of UserStat */ ],\n" +
        "      \"TopTrendingTopics\": [ /* array of strings */ ]\n" +
        "    }\n" +
        "  ]\n" +
        "}\n\n" +
        "Here is the JSON schema for a single UserStat object:\n" +
        "{\n" +
        "  \"GuildId\":           number,    // Discord guild ID\n" +
        "  \"UserId\":            number,    // Discord user ID\n" +
        "  \"SentimentScore\":    number,    // (–1.0 to +1.0)\n" +
        "  \"AuthoritarianLeft\": number,    // percentage 0–100\n" +
        "  \"AuthoritarianRight\":number,\n" +
        "  \"LibertarianLeft\":   number,\n" +
        "  \"LibertarianRight\":  number,\n" +
        "  \"TotalMessages\":     integer,\n" +
        "  \"PeakActivityHour\":  integer,   // hour of day 0–23\n" +
        "  \"ExternalLinkShares\":integer,\n" +
        "  \"BiasShiftAl\":       number,    // week-over-week delta\n" +
        "  \"BiasShiftAr\":       number,\n" +
        "  \"BiasShiftLl\":       number,\n" +
        "  \"BiasShiftLr\":       number\n" +
        "}\n\n" +
        "Respond with exactly that JSON object and no additional text.";

    public async Task<string?> AnalyzeStats(string json)
    {
        var msgs = new ChatMessage[]
        {
            ChatMessage.CreateSystemMessage(_systemPrompt),
            ChatMessage.CreateUserMessage("Here is the dataset:\n" + json)
        };

        var requestOptions = new ChatCompletionOptions()
        {
            Temperature = 0.5f,
            TopP = 1f,
        };

        var completion = await client.CompleteChatAsync(msgs, requestOptions);
        if (completion is { Value: { } value })
            return value.Content.ToString();

        logger.LogError("Failed to send llm request");
        return null;
    }
}