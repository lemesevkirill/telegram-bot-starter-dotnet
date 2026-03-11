namespace BotTemplate.Api.LLM;

public interface ILLMService
{
    Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken ct);
}
