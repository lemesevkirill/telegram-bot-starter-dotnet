using BotTemplate.Core.Jobs;
using Microsoft.EntityFrameworkCore;

namespace BotTemplate.Api;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(entity =>
        {
            entity.Property(x => x.Id).UseIdentityByDefaultColumn();
            entity.Property(x => x.UpdateId).HasColumnType("bigint").IsRequired();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Attempts).HasDefaultValue(0);
            entity.Property(x => x.LastError).HasColumnType("text");
            entity.Property(x => x.UpdatePayload).HasColumnType("text");
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.Property(x => x.UpdatedAt).HasColumnType("timestamp with time zone");
            entity.HasIndex(x => x.UpdateId).IsUnique();
            entity.HasIndex(x => new { x.Status, x.Attempts, x.Id }).HasDatabaseName("IX_Jobs_Status_Attempts_Id");
        });
    }
}
