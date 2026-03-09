using System.Text.Json;
using BotTemplate.Api.LLM;
using BotTemplate.Api.Messaging;
using BotTemplate.Api.Services;
using BotTemplate.Api.TTS;
using BotTemplate.Core.Jobs;
using Telegram.Bot.Types;

namespace BotTemplate.Api.Execution;

public sealed class TelegramJobExecutor(
    ILogger<TelegramJobExecutor> logger,
    TelegramSender telegramSender,
    ILLMService llmService,
    ITTSService ttsService) : IJobExecutor
{
    public async Task ExecuteAsync(Job job, CancellationToken ct)
    {
        logger.LogInformation("Processing started {JobId}", job.Id);

        var update = JsonSerializer.Deserialize<Update>(
            job.UpdatePayload,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        var text = update?.Message?.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            logger.LogInformation("Message text is null or whitespace {JobId}", job.Id);
            return;
        }

        logger.LogInformation("Sending typing indicator {JobId}", job.Id);
        await telegramSender.SendTypingAsync(job.ChatId, ct);

        var llmResult = await llmService.TranslateToGermanAsync(text, ct);

        // logger.LogInformation("Sending translated text message {JobId}", job.Id);
        // await telegramSender.SendTextMessageAsync(job.ChatId, llmResult.Text, ct);

        logger.LogInformation("Generating TTS audio {JobId}", job.Id);
        using var audio = await ttsService.SynthesizeAsync(llmResult.Text, ct);

        var audioMessage = new AudioMessage
        {
            Audio = audio,
            Title = "German Translation",
            Performer = "HearTheText",
            Caption = llmResult.Text,
            FileName = "translation filename without ext"
        };

        logger.LogInformation("Sending Telegram audio {JobId}", job.Id);
        await telegramSender.SendAudioAsync(job.ChatId, audioMessage, ct);
    }
}
