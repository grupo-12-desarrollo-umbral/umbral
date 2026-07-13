using System.Reflection;
using MassTransit;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<LiveSession> LiveSessions => Set<LiveSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // MassTransit transactional bus outbox tables (InboxState/OutboxMessage/OutboxState). The
        // outbox-backed IPublishEndpoint inserts OutboxMessage rows in the same SaveChanges as the
        // business write; a hosted delivery service drains them to RabbitMQ. This service only
        // publishes, so InboxState stays empty but is mapped to keep the standard model shape.
        builder.AddTransactionalOutboxEntities();
    }
}
