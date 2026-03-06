using System.Text.Json;
using BotTemplate.Core.Configuration;
using BotTemplate.Core.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace BotTemplate.Api.Endpoints;

public static class TelegramWebhookEndpoint
{
    public static IEndpointRouteBuilder MapTelegramWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("/telegram/webhook", HandleAsync);
        return app;
    }

    private static async Task<IResult> HandleAsync(HttpRequest request, AppDbContext dbContext, IOptions<TelegramOptions> telegramOptions)
    {
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

        var chatId = update?.Message?.Chat.Id;
        var userId = update?.Message?.From?.Id;

        if (chatId is null || userId is null)
        {
            return Results.BadRequest();
        }

        var now = DateTime.UtcNow;
        var job = new Job
        {
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
        await dbContext.SaveChangesAsync();

        return Results.Ok();
    }
}
