namespace BotTemplate.Api.LLM;

public interface ILLMService
{
    Task<LLMResult> TranslateToGermanAsync(string text, CancellationToken ct);
}
