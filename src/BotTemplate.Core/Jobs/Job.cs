namespace BotTemplate.Core.Jobs;

public sealed class Job
{
    public long Id { get; set; }

    public long ChatId { get; set; }

    public long UserId { get; set; }

    public string UpdatePayload { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
