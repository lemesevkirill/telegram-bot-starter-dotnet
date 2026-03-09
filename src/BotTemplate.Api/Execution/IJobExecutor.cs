using BotTemplate.Core.Jobs;

namespace BotTemplate.Api.Execution;

public interface IJobExecutor
{
    Task ExecuteAsync(Job job, CancellationToken ct);
}
