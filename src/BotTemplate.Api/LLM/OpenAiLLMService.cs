using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BotTemplate.Api.LLM;

public sealed class OpenAiLLMService(
    HttpClient httpClient,
    IOptions<LLMOptions> llmOptionsAccessor,
    PromptBuilder promptBuilder) : ILLMService
{
    private readonly LLMOptions llmOptions = llmOptionsAccessor.Value;

    public async Task<LLMResult> TranslateToGermanAsync(string text, CancellationToken ct)
    {
        var prompt = promptBuilder.BuildGermanTranslationPrompt(text);
        var payload = JsonSerializer.Serialize(new
        {
            model = llmOptions.Model,
            input = prompt
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", llmOptions.ApiKey);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(ct);
        var textResult = ExtractText(responseContent);

        if (string.IsNullOrWhiteSpace(textResult))
        {
            throw new InvalidOperationException("OpenAI response did not contain output text.");
        }

        return new LLMResult
        {
            Text = textResult
        };
    }

    private static string? ExtractText(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("output_text", out var outputText) &&
            outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString();
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var outputItem in output.EnumerateArray())
        {
            if (!outputItem.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }
}
