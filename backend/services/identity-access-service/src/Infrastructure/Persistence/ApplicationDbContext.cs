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

    public DbSet<IdentityProviderSession> IdentityProviderSessions => Set<IdentityProviderSession>();

    public DbSet<Team> Teams => Set<Team>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
