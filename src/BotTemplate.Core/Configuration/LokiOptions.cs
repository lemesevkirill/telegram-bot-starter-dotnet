namespace BotTemplate.Core.Configuration;

public sealed class LokiOptions
{
    public const string SectionName = "Loki";

    public bool Enabled { get; init; } = false;

    public string Endpoint { get; init; } = "";

    public string Username { get; init; } = "";

    public string Password { get; init; } = "";

    public string ServiceName { get; init; } = "telegram-bot";
}
