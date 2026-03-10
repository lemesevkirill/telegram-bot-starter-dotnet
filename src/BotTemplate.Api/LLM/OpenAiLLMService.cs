using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BotTemplate.Core.Execution;
using Microsoft.Extensions.Options;

namespace BotTemplate.Api.LLM;

public sealed class OpenAiLLMService(
    ILogger<OpenAiLLMService> logger,
    HttpClient httpClient,
    IOptions<LLMOptions> llmOptionsAccessor,
    PromptBuilder promptBuilder) : ILLMService
{
    private readonly LLMOptions llmOptions = llmOptionsAccessor.Value;

    public async Task<string> TranslateAsync(JobContext ctx, string text, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        Metrics.LlmRequestsTotal.Add(
            1,
            new KeyValuePair<string, object?>("component", "llm"),
            new KeyValuePair<string, object?>("operation", "translate_to_german"),
            new KeyValuePair<string, object?>("model", llmOptions.Model),
            new KeyValuePair<string, object?>("result", "started"));

        logger.LogInformation(
            "llm_started component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
            "llm",
            "translate_to_german",
            "started",
            ctx.JobId,
            ctx.UpdateId,
            ctx.ChatId,
            ctx.Attempt,
            0d);

        try
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

            Metrics.LlmLatencySeconds.Record(
                stopwatch.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("component", "llm"),
                new KeyValuePair<string, object?>("operation", "translate_to_german"),
                new KeyValuePair<string, object?>("model", llmOptions.Model),
                new KeyValuePair<string, object?>("result", "success"));

            logger.LogInformation(
                "llm_completed component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
                "llm",
                "translate_to_german",
                "success",
                ctx.JobId,
                ctx.UpdateId,
                ctx.ChatId,
                ctx.Attempt,
                stopwatch.Elapsed.TotalMilliseconds);

            return textResult;
        }
        catch (Exception ex)
        {
            Metrics.LlmErrorsTotal.Add(
                1,
                new KeyValuePair<string, object?>("component", "llm"),
                new KeyValuePair<string, object?>("operation", "translate_to_german"),
                new KeyValuePair<string, object?>("model", llmOptions.Model),
                new KeyValuePair<string, object?>("result", "error"));

            logger.LogError(
                ex,
                "llm_failed component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
                "llm",
                "translate_to_german",
                "failed",
                ctx.JobId,
                ctx.UpdateId,
                ctx.ChatId,
                ctx.Attempt,
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
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
