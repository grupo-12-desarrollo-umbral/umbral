using System.Reflection;

namespace umbral_backend.Infrastructure.Persistence;

/// <summary>
/// The ScoringMonitoring persistence root. It ships with an empty model in this slice (#191): the
/// initial migration creates only <c>__EFMigrationsHistory</c>. The first aggregate
/// (<c>ScoreEntry</c>) and its configuration arrive in HU-37.
/// </summary>
public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
