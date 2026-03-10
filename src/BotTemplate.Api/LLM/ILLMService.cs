using BotTemplate.Core.Execution;

namespace BotTemplate.Api.LLM;

public interface ILLMService
{
    Task<string> TranslateAsync(JobContext ctx, string text, CancellationToken ct);
}
