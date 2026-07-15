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

    public DbSet<Penalty> Penalties => Set<Penalty>();

    public DbSet<SessionOperatorAssignmentProjection> SessionOperatorAssignments => Set<SessionOperatorAssignmentProjection>();

    public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
