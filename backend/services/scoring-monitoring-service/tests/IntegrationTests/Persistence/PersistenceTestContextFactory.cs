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

    public ScoringMonitoringDbContext Create(IMediator? mediator = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
            .UseNpgsql(_connectionString);

        optionsBuilder.AddInterceptors(
            new AuditableEntityInterceptor(TimeProvider.System),
            new DispatchDomainEventsInterceptor(mediator ?? new NoOpMediator()));

        return new ScoringMonitoringDbContext(optionsBuilder.Options);
    }
}
