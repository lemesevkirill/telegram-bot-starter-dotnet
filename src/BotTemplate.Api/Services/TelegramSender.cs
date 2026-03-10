using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using BotTemplate.Api.Messaging;
using BotTemplate.Core.Execution;
using System.Diagnostics;

namespace BotTemplate.Api.Services;

public sealed class TelegramSender(TelegramBotClient botClient, ILogger<TelegramSender> logger)
{
    public Task SendTypingAsync(JobContext ctx, long chatId, CancellationToken cancellationToken = default)
    {
        return SendWithMetricsAsync(
            ctx,
            chatId,
            "typing",
            ct => botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct),
            cancellationToken);
    }

    public Task SendTextMessageAsync(JobContext ctx, long chatId, string text, CancellationToken cancellationToken = default)
    {
        return SendWithMetricsAsync(
            ctx,
            chatId,
            "text",
            ct => botClient.SendMessage(chatId, text, cancellationToken: ct),
            cancellationToken);
    }

    public Task SendAudioAsync(JobContext ctx, long chatId, AudioMessage message, CancellationToken cancellationToken = default)
    {
        return SendWithMetricsAsync(
            ctx,
            chatId,
            "audio",
            ct => botClient.SendAudio(
                chatId,
                new InputFileStream(message.Audio, message.FileName),
                caption: message.Caption,
                performer: message.Performer,
                title: message.Title,
                cancellationToken: ct),
            cancellationToken);
    }

    private async Task SendWithMetricsAsync(
        JobContext ctx,
        long chatId,
        string operation,
        Func<CancellationToken, Task> sendAction,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        Metrics.TelegramSendTotal.Add(
            1,
            new KeyValuePair<string, object?>("component", "telegram_sender"),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", "started"));

        logger.LogInformation(
            "telegram_send_started component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
            "telegram_sender",
            operation,
            "started",
            ctx.JobId,
            ctx.UpdateId,
            ctx.ChatId,
            ctx.Attempt,
            0d);

        try
        {
            await sendAction(cancellationToken);

            Metrics.TelegramSendLatencySeconds.Record(
                stopwatch.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("component", "telegram_sender"),
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("result", "success"));

            logger.LogInformation(
                "telegram_send_completed component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
                "telegram_sender",
                operation,
                "success",
                ctx.JobId,
                ctx.UpdateId,
                ctx.ChatId,
                ctx.Attempt,
                stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            Metrics.TelegramSendErrorsTotal.Add(
                1,
                new KeyValuePair<string, object?>("component", "telegram_sender"),
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("result", "error"));

            logger.LogError(
                ex,
                "telegram_send_failed component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
                "telegram_sender",
                operation,
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
