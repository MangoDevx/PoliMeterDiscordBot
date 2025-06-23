namespace PoliMeterDiscordBot.Models;

public sealed class BotSettings
{
    public required string Token { get; set; } = null!;
    public required string LlmToken { get; set; } = null!;
    public required string LlmEndpoint { get; set; } = null!;
    public bool UseLlm { get; set; } = false;
}