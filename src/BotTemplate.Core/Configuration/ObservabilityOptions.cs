using System.ComponentModel.DataAnnotations;

namespace BotTemplate.Core.Configuration;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    [Required]
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    public string OtlpApiKey { get; set; } = string.Empty;

    [Required]
    public string ServiceName { get; set; } = "telegram-bot";
}
