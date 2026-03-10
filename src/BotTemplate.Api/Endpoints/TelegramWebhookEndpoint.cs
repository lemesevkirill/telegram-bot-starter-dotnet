using System.Text.Json;
using BotTemplate.Core.Configuration;
using BotTemplate.Core.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Npgsql;
using Telegram.Bot.Types;
using System.Diagnostics;

namespace BotTemplate.Api.Endpoints;

public static class TelegramWebhookEndpoint
{
    public static IEndpointRouteBuilder MapTelegramWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("/telegram/webhook", HandleAsync);
        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        AppDbContext dbContext,
        IOptions<TelegramOptions> telegramOptions,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var secretTokenHeader) ||
            secretTokenHeader != telegramOptions.Value.WebhookSecret)
        {
            return Results.Unauthorized();
        }

        using var reader = new StreamReader(request.Body);
        var rawBody = await reader.ReadToEndAsync();

        Update? update;

        try
        {
            update = JsonSerializer.Deserialize<Update>(rawBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        var updateId = update?.Id;
        var chatId = update?.Message?.Chat.Id;
        var userId = update?.Message?.From?.Id;

        if (updateId is null || chatId is null || userId is null)
        {
            return Results.BadRequest();
        }

        var now = DateTime.UtcNow;
        var job = new Job
        {
            UpdateId = updateId.Value,
            ChatId = chatId.Value,
            UserId = userId.Value,
            UpdatePayload = rawBody,
            Status = JobStatus.Pending,
            Attempts = 0,
            LastError = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Jobs.Add(job);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            logger.LogInformation(
                "job_created component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
                "webhook",
                "create_job",
                "duplicate",
                0L,
                updateId.Value,
                chatId.Value,
                0,
                stopwatch.Elapsed.TotalMilliseconds);
            return Results.Ok();
        }

        Metrics.JobsCreatedTotal.Add(
            1,
            new KeyValuePair<string, object?>("component", "webhook"),
            new KeyValuePair<string, object?>("operation", "create_job"),
            new KeyValuePair<string, object?>("result", "success"));

        logger.LogInformation(
            "job_created component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
            "webhook",
            "create_job",
            "success",
            job.Id,
            job.UpdateId,
            job.ChatId,
            0,
            stopwatch.Elapsed.TotalMilliseconds);

        return Results.Ok();
    }
}
