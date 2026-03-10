using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BotTemplate.Core.Execution;
using Microsoft.Extensions.Options;

namespace BotTemplate.Api.TTS;

public sealed class OpenAiTTSService(
    ILogger<OpenAiTTSService> logger,
    HttpClient httpClient,
    IOptions<TTSOptions> ttsOptionsAccessor) : ITTSService
{
    private readonly TTSOptions ttsOptions = ttsOptionsAccessor.Value;

    public async Task<Stream> GenerateAsync(JobContext ctx, string text, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        Metrics.TtsRequestsTotal.Add(
            1,
            new KeyValuePair<string, object?>("component", "tts"),
            new KeyValuePair<string, object?>("operation", "synthesize"),
            new KeyValuePair<string, object?>("model", ttsOptions.Model),
            new KeyValuePair<string, object?>("result", "started"));

        logger.LogInformation(
            "tts_started component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
            "tts",
            "synthesize",
            "started",
            ctx.JobId,
            ctx.UpdateId,
            ctx.ChatId,
            ctx.Attempt,
            0d);

        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("TTS input text is empty.");
            }

            var synthesisInput = text.Length > ttsOptions.MaxInputLength
                ? text[..ttsOptions.MaxInputLength]
                : text;

            if (synthesisInput.Length != text.Length)
            {
                logger.LogWarning(
                    "TTS input was truncated from {OriginalLength} to {MaxLength} characters",
                    text.Length,
                    ttsOptions.MaxInputLength);
            }

            var payload = JsonSerializer.Serialize(new
            {
                model = ttsOptions.Model,
                voice = ttsOptions.Voice,
                input = synthesisInput,
                format = ttsOptions.Format
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "audio/speech");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ttsOptions.ApiKey);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(ct);
            var output = new MemoryStream();
            await responseStream.CopyToAsync(output, ct);
            output.Position = 0;

            Metrics.TtsLatencySeconds.Record(
                stopwatch.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("component", "tts"),
                new KeyValuePair<string, object?>("operation", "synthesize"),
                new KeyValuePair<string, object?>("model", ttsOptions.Model),
                new KeyValuePair<string, object?>("result", "success"));

            logger.LogInformation(
                "tts_completed component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
                "tts",
                "synthesize",
                "success",
                ctx.JobId,
                ctx.UpdateId,
                ctx.ChatId,
                ctx.Attempt,
                stopwatch.Elapsed.TotalMilliseconds);

            return output;
        }
        catch (Exception ex)
        {
            Metrics.TtsErrorsTotal.Add(
                1,
                new KeyValuePair<string, object?>("component", "tts"),
                new KeyValuePair<string, object?>("operation", "synthesize"),
                new KeyValuePair<string, object?>("model", ttsOptions.Model),
                new KeyValuePair<string, object?>("result", "error"));

            logger.LogError(
                ex,
                "tts_failed component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
                "tts",
                "synthesize",
                "failed",
                ctx.JobId,
                ctx.UpdateId,
                ctx.ChatId,
                ctx.Attempt,
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}
