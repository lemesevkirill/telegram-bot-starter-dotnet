using System.ComponentModel.DataAnnotations;

namespace BotTemplate.Core.Configuration;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    [Range(1, int.MaxValue)]
    public int PollIntervalMs { get; set; } = 1000;

    [Range(1, int.MaxValue)]
    public int MaxConcurrentJobs { get; set; } = 4;

    [Range(1, int.MaxValue)]
    public int MaxAttempts { get; set; } = 2;

    [Range(1, int.MaxValue)]
    public int MaxJobAgeMinutes { get; set; } = 30;
}
