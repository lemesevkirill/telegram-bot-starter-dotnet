using BotTemplate.Core.Jobs;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BotTemplate.Api;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var executionOptionsComparer = new ValueComparer<Dictionary<string, string>>(
            (left, right) => AreExecutionOptionsEqual(left, right),
            dictionary => GetExecutionOptionsHashCode(dictionary),
            dictionary => CloneExecutionOptions(dictionary));

        modelBuilder.Entity<Job>(entity =>
        {
            entity.Property(x => x.Id).UseIdentityByDefaultColumn();
            entity.Property(x => x.UpdateId).HasColumnType("bigint").IsRequired();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Attempts).HasDefaultValue(0);
            entity.Property(x => x.LastError).HasColumnType("text");
            entity.Property(x => x.UpdatePayload).HasColumnType("text");
            entity.Property(x => x.ExecutionOptions)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'{}'::jsonb")
                .HasConversion(
                    value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                    value => string.IsNullOrWhiteSpace(value)
                        ? new Dictionary<string, string>()
                        : JsonSerializer.Deserialize<Dictionary<string, string>>(value) ?? new Dictionary<string, string>())
                .Metadata.SetValueComparer(executionOptionsComparer);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
            entity.HasIndex(x => x.UpdateId).IsUnique();
            entity.HasIndex(x => new { x.Status, x.Attempts, x.Id }).HasDatabaseName("IX_Jobs_Status_Attempts_Id");
        });
    }

    private static bool AreExecutionOptionsEqual(
        Dictionary<string, string>? left,
        Dictionary<string, string>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) || value != pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static int GetExecutionOptionsHashCode(Dictionary<string, string> dictionary)
    {
        var hash = new HashCode();

        foreach (var pair in dictionary.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    private static Dictionary<string, string> CloneExecutionOptions(Dictionary<string, string> dictionary)
    {
        return dictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}
