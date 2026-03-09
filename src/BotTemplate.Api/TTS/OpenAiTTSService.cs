using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BotTemplate.Api.TTS;

public sealed class OpenAiTTSService(
    ILogger<OpenAiTTSService> logger,
    HttpClient httpClient,
    IOptions<TTSOptions> ttsOptionsAccessor) : ITTSService
{
    private readonly TTSOptions ttsOptions = ttsOptionsAccessor.Value;

    public async Task<Stream> SynthesizeAsync(string text, CancellationToken ct)
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

        return output;
    }
}
