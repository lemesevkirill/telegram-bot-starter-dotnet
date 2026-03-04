using System.ComponentModel.DataAnnotations;

namespace BotTemplate.Core.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
