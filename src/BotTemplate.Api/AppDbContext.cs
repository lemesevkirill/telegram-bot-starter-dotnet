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
            entity.Property(x => x.UpdatePayload).HasColumnType("text");
        });
    }
}
