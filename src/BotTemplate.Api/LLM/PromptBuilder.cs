namespace BotTemplate.Api.LLM;

public sealed class PromptBuilder
{
    public string BuildGermanTranslationPrompt(string text)
    {
        return
            """
            Translate the following text to German.
            Return only the translated text.

            Text:
            """ + $"\n{text}";
    }
}
