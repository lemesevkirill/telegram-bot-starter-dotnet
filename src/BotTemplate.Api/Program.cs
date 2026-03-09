using BotTemplate.Api.Endpoints;
using BotTemplate.Api.Execution;
using BotTemplate.Api.LLM;
using BotTemplate.Api.Services;
using BotTemplate.Api.TTS;
using BotTemplate.Api.Workers;
using BotTemplate.Core.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace BotTemplate.Api;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        });

        ConfigureServices(builder);

        var app = builder.Build();

        RunMigrations(app);

        ConfigurePipeline(app);

        LogStartupDiagnostics(app);

        app.Run();
    }

    private static void RunMigrations(WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                logger.LogInformation("RunMigrations START");

                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();

                logger.LogInformation("RunMigrations END");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Database migration failed");
                throw;
            }
        }
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<TelegramOptions>()
            .Bind(builder.Configuration.GetSection(TelegramOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services
            .AddOptions<DatabaseOptions>()
            .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services
            .AddOptions<WorkerOptions>()
            .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services
            .AddOptions<LLMOptions>()
            .Bind(builder.Configuration.GetSection(LLMOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services
            .AddOptions<TTSOptions>()
            .Bind(builder.Configuration.GetSection(TTSOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(databaseOptions.ConnectionString);
        });

        builder.Services.AddSingleton<TelegramBotClient>(serviceProvider =>
        {
            var telegramOptions = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
            return new TelegramBotClient(telegramOptions.BotToken);
        });

        builder.Services.AddSingleton<TelegramSender>();
        builder.Services.AddScoped<PromptBuilder>();
        builder.Services.AddHttpClient<OpenAiLLMService>((serviceProvider, client) =>
        {
            var llmOptions = serviceProvider.GetRequiredService<IOptions<LLMOptions>>().Value;
            client.BaseAddress = new Uri(llmOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(llmOptions.TimeoutSeconds);
        });
        builder.Services.AddScoped<ILLMService>(serviceProvider => serviceProvider.GetRequiredService<OpenAiLLMService>());
        builder.Services.AddHttpClient<OpenAiTTSService>((serviceProvider, client) =>
        {
            var ttsOptions = serviceProvider.GetRequiredService<IOptions<TTSOptions>>().Value;
            client.BaseAddress = new Uri("https://api.openai.com/v1/");
            client.Timeout = TimeSpan.FromSeconds(ttsOptions.TimeoutSeconds);
        });
        builder.Services.AddScoped<ITTSService>(serviceProvider => serviceProvider.GetRequiredService<OpenAiTTSService>());
        builder.Services.AddScoped<IJobExecutor, TelegramJobExecutor>();

        builder.Services.AddHostedService<JobWorker>();
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Json(new { status = "ok" }));
        app.MapGet("/health/ready", () => Results.Json(new { status = "ok" }));
        app.MapTelegramWebhook();
    }

    private static void LogStartupDiagnostics(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var telegram = app.Services.GetRequiredService<IOptions<TelegramOptions>>().Value;
        var database = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var worker = app.Services.GetRequiredService<IOptions<WorkerOptions>>().Value;
        var llm = app.Services.GetRequiredService<IOptions<LLMOptions>>().Value;
        var tts = app.Services.GetRequiredService<IOptions<TTSOptions>>().Value;

        logger.LogInformation("[CONFIG] Environment = {Value}", app.Environment.EnvironmentName);
        logger.LogInformation("[CONFIG] ASPNETCORE_URLS = {Value}", Prefix(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")));
        logger.LogInformation("[CONFIG] Telegram.BotToken = {Value}", Prefix(telegram.BotToken));
        logger.LogInformation("[CONFIG] Telegram.WebhookSecret = {Value}", Prefix(telegram.WebhookSecret));
        logger.LogInformation("[CONFIG] Database.ConnectionString = {Value}", Prefix(database.ConnectionString));
        logger.LogInformation("[CONFIG] Worker.PollIntervalMs = {Value}", worker.PollIntervalMs);
        logger.LogInformation("[CONFIG] Worker.MaxConcurrentJobs = {Value}", worker.MaxConcurrentJobs);
        logger.LogInformation("[CONFIG] Worker.MaxAttempts = {Value}", worker.MaxAttempts);
        logger.LogInformation("[CONFIG] Worker.MaxJobAgeMinutes = {Value}", worker.MaxJobAgeMinutes);
        logger.LogInformation("[CONFIG] LLM.Model = {Value}", llm.Model);
        logger.LogInformation("[CONFIG] LLM.BaseUrl = {Value}", llm.BaseUrl);
        logger.LogInformation("[CONFIG] LLM.TimeoutSeconds = {Value}", llm.TimeoutSeconds);
        logger.LogInformation("[CONFIG] LLM.ApiKey = {Value}", Prefix(llm.ApiKey));
        logger.LogInformation("[CONFIG] TTS.Model = {Value}", tts.Model);
        logger.LogInformation("[CONFIG] TTS.Voice = {Value}", tts.Voice);
        logger.LogInformation("[CONFIG] TTS.Format = {Value}", tts.Format);
        logger.LogInformation("[CONFIG] TTS.TimeoutSeconds = {Value}", tts.TimeoutSeconds);
        logger.LogInformation("[CONFIG] TTS.MaxInputLength = {Value}", tts.MaxInputLength);
        logger.LogInformation("[CONFIG] TTS.ApiKey = {Value}", Prefix(tts.ApiKey));
    }

    private static string Prefix(string? value)
    {
        if (value is null)
        {
            return "<missing>";
        }

        if (value.Length == 0)
        {
            return "<empty>";
        }

        if (value.Length <= 15)
        {
            return value;
        }

        return value[..15] + "...";
    }
}
