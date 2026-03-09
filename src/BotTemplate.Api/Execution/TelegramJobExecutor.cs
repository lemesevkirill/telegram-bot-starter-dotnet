using System.Text.Json;
using BotTemplate.Api.LLM;
using BotTemplate.Api.Services;
using BotTemplate.Core.Jobs;
using Telegram.Bot.Types;

namespace BotTemplate.Api.Execution;

public sealed class TelegramJobExecutor(
    ILogger<TelegramJobExecutor> logger,
    TelegramSender telegramSender,
    ILLMService llmService) : IJobExecutor
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

        if (text is null)
        {
            logger.LogInformation("Message text is null {JobId}", job.Id);
            return;
        }

        logger.LogInformation("Sending typing indicator {JobId}", job.Id);
        await telegramSender.SendTypingAsync(job.ChatId, ct);

        var llmResult = await llmService.TranslateToGermanAsync(text, ct);

        logger.LogInformation("Sending Telegram message {JobId}", job.Id);
        await telegramSender.SendTextMessageAsync(job.ChatId, llmResult.Text, ct);
    }
}
