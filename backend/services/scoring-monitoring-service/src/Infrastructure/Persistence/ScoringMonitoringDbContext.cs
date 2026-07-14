using System.Reflection;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence;

public sealed class ScoringMonitoringDbContext : DbContext
{
    public ScoringMonitoringDbContext(DbContextOptions<ScoringMonitoringDbContext> options)
        : base(options)
    {
    }

    public DbSet<ScoreEntry> ScoreEntries => Set<ScoreEntry>();

    public DbSet<Ranking> Rankings => Set<Ranking>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
