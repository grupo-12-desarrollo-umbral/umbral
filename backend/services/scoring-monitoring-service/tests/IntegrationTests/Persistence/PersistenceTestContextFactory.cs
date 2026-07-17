using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Common;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Interceptors;

namespace umbral_backend.ScoringMonitoring.IntegrationTests.Persistence;

internal sealed class PersistenceTestContextFactory
{
    private readonly string _connectionString;

    public PersistenceTestContextFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public ScoringMonitoringDbContext Create(
        IMediator? mediator = null,
        IOutboxDomainEventDispatcher? outboxDispatcher = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
            .UseNpgsql(_connectionString);

        optionsBuilder.AddInterceptors(
            new AuditableEntityInterceptor(new StubCurrentUser(), TimeProvider.System),
            new DispatchDomainEventsInterceptor(
                mediator ?? new NoOpMediator(),
                CreateServiceProvider(outboxDispatcher ?? new NoOpOutboxDomainEventDispatcher())));

        return new ScoringMonitoringDbContext(optionsBuilder.Options);
    }

    private static IServiceProvider CreateServiceProvider(IOutboxDomainEventDispatcher outboxDispatcher)
    {
        var services = new ServiceCollection();
        services.AddSingleton(outboxDispatcher);
        return services.BuildServiceProvider();
    }

    private sealed class NoOpOutboxDomainEventDispatcher : IOutboxDomainEventDispatcher
    {
        public Task DispatchAsync(BaseEvent domainEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public string? Id => "integration-test";
        public string? Email => null;
        public string? Role => null;
    }
}
