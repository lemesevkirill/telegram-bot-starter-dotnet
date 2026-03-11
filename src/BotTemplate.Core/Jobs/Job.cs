namespace BotTemplate.Core.Jobs;

public sealed class Job
{
    public long Id { get; set; }

    public long UpdateId { get; set; }

    public long ChatId { get; set; }

    public long UserId { get; set; }

    public string UpdatePayload { get; set; } = string.Empty;

    public Dictionary<string, string> ExecutionOptions { get; set; } = new();

    public JobStatus Status { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
