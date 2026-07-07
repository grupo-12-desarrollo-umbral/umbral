using System.Reflection;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();

    public DbSet<LiveSessionReference> LiveSessionReferences => Set<LiveSessionReference>();

    public DbSet<SessionTeamAssociation> SessionTeamAssociations => Set<SessionTeamAssociation>();

    public DbSet<JoinToken> JoinTokens => Set<JoinToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
