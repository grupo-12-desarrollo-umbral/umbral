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

    public DbSet<RegisteredTeam> RegisteredTeams => Set<RegisteredTeam>();

    public DbSet<RegisteredTeamMembership> RegisteredTeamMemberships => Set<RegisteredTeamMembership>();

    public DbSet<LiveSessionReference> LiveSessionReferences => Set<LiveSessionReference>();

    public DbSet<SessionTeamAssociation> SessionTeamAssociations => Set<SessionTeamAssociation>();

    public DbSet<JoinToken> JoinTokens => Set<JoinToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
