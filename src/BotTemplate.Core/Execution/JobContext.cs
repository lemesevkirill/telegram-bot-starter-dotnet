namespace BotTemplate.Core.Execution;

public sealed class JobContext
{
    public long JobId { get; init; }

    public long UpdateId { get; init; }

    public long ChatId { get; init; }

    public int Attempt { get; init; }

    public Dictionary<string, string> ExecutionOptions { get; init; } = new();
}
