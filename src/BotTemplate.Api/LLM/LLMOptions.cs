using System.ComponentModel.DataAnnotations;

namespace BotTemplate.Api.LLM;

public sealed class LLMOptions
{
    public const string SectionName = "LLM";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = "gpt-4.1-mini";

    [Required]
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 60;
}
