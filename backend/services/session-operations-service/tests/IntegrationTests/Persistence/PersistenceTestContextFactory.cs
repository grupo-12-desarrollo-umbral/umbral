using MediatR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Persistence.Interceptors;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

internal sealed class PersistenceTestContextFactory
{
    private readonly string _connectionString;

    public PersistenceTestContextFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public ApplicationDbContext Create(IMediator? mediator = null, ICurrentUser? currentUser = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connectionString);

        var effectiveCurrentUser = currentUser ?? TestCurrentUser.Default;

        optionsBuilder.AddInterceptors(
            new AuditableEntityInterceptor(effectiveCurrentUser, TimeProvider.System),
            new DispatchDomainEventsInterceptor(mediator ?? new NoOpMediator()));

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
