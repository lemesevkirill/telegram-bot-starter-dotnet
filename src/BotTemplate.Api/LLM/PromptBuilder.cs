namespace BotTemplate.Api.LLM;

public sealed class PromptBuilder
{
    public string BuildTranslationPrompt(string text, string targetLanguage)
    {
        return
            $"""
            Translate the following text to {targetLanguage}.
            Return only the translated text.

            Text:
            {text}
            """;
    }
}
