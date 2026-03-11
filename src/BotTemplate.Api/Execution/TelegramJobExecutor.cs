using System.Text.Json;
using BotTemplate.Api.LLM;
using BotTemplate.Api.Messaging;
using BotTemplate.Api.Services;
using BotTemplate.Api.TTS;
using BotTemplate.Core.Execution;
using Telegram.Bot.Types;

namespace BotTemplate.Api.Execution;

public sealed class TelegramJobExecutor(
    ILogger<TelegramJobExecutor> logger,
    TelegramSender telegramSender,
    ILLMService llmService,
    ITTSService ttsService) : IJobExecutor
{
    public async Task ExecuteAsync(JobContext ctx, string payload, CancellationToken ct)
    {
        logger.LogInformation("Processing started {JobId}", ctx.JobId);

        var update = JsonSerializer.Deserialize<Update>(
            payload,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        var text = update?.Message?.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            logger.LogInformation("Message text is null or whitespace {JobId}", ctx.JobId);
            return;
        }

        logger.LogInformation("Sending typing indicator {JobId}", ctx.JobId);
        await telegramSender.SendTypingAsync(ctx, ctx.ChatId, ct);

        var targetLanguage = ctx.ExecutionOptions.TryGetValue("targetLanguage", out var configuredLanguage) &&
                             !string.IsNullOrWhiteSpace(configuredLanguage)
            ? configuredLanguage
            : "en";
        var translated = await llmService.TranslateAsync(text, targetLanguage, ct);

        logger.LogInformation("Generating TTS audio {JobId}", ctx.JobId);
        using var audio = await ttsService.GenerateAsync(ctx, translated, ct);

        var audioMessage = new AudioMessage
        {
            Audio = audio,
            Title = "Translation",
            Performer = "HearTheText",
            Caption = translated,
            FileName = "translation.mp3"
        };

        logger.LogInformation("Sending Telegram audio {JobId}", ctx.JobId);
        await telegramSender.SendAudioAsync(ctx, ctx.ChatId, audioMessage, ct);
    }
}
