using BotTemplate.Api.Endpoints;
using BotTemplate.Core.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

        builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(databaseOptions.ConnectionString);
        });
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

        logger.LogInformation("[CONFIG] Environment = {Value}", app.Environment.EnvironmentName);
        logger.LogInformation("[CONFIG] ASPNETCORE_URLS = {Value}", Prefix(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")));
        logger.LogInformation("[CONFIG] Telegram.BotToken = {Value}", Prefix(telegram.BotToken));
        logger.LogInformation("[CONFIG] Telegram.WebhookSecret = {Value}", Prefix(telegram.WebhookSecret));
        logger.LogInformation("[CONFIG] Database.ConnectionString = {Value}", Prefix(database.ConnectionString));
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
