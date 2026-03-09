namespace BotTemplate.Api.Messaging;

public sealed class AudioMessage
{
    public required Stream Audio { get; init; }

    public string? Title { get; init; }

    public string? Performer { get; init; }

    public string? Caption { get; init; }

    public string FileName { get; init; } = "audio.mp3";
}
