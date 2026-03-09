using System.ComponentModel.DataAnnotations;

namespace BotTemplate.Api.TTS;

public sealed class TTSOptions
{
    public const string SectionName = "TTS";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = "gpt-4o-mini-tts";

    [Required]
    public string Voice { get; set; } = "alloy";

    [Required]
    public string Format { get; set; } = "mp3";

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(1, 5000)]
    public int MaxInputLength { get; set; } = 1000;
}
