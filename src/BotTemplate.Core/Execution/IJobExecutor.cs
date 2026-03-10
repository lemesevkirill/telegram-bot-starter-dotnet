namespace BotTemplate.Core.Execution;

public interface IJobExecutor
{
    Task ExecuteAsync(JobContext ctx, string payload, CancellationToken ct);
}
