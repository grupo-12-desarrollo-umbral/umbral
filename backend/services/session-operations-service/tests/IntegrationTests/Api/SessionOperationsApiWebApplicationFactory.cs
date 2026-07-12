using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Realtime;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

public sealed class SessionOperationsApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__umbral_backendDb";
    private readonly string _connectionString;

    public SessionOperationsApiWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, _connectionString);
    }

    public FakeParticipantMembershipAccessClient AccessClient { get; } = new();

    public FakeAssignableSessionOperatorAccessClient AssignableSessionOperatorAccessClient { get; } = new();

    public FakeMissionReadinessSource MissionReadinessSource { get; } = new();

    public FakeMissionRuntimeSource MissionRuntimeSource { get; } = new();

    public FakeAuthenticatedActorProfileAccessClient AuthenticatedActorProfileAccessClient { get; } = new();

    public FakeTeamReferenceCatalogClient TeamCatalogClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            // Remove the real AuthoritativeSessionTimerWorker hosted service. It runs on a 1s
            // PeriodicTimer using TimeProvider.System, so under the booted host it would race the
            // tests that drive the worker deterministically via their own FixedTimeProvider instance
            // (DES-91), mutating seeded session timer state concurrently. Tests that need a tick
            // construct and await the worker themselves.
            foreach (var timerWorkerDescriptor in services
                .Where(descriptor => descriptor.ImplementationType == typeof(AuthoritativeSessionTimerWorker))
                .ToList())
            {
                services.Remove(timerWorkerDescriptor);
            }

            services.RemoveAll<IParticipantMembershipAccessClient>();
            services.AddScoped<IParticipantMembershipAccessClient>(_ => AccessClient);
            services.RemoveAll<IAssignableSessionOperatorAccessClient>();
            services.AddScoped<IAssignableSessionOperatorAccessClient>(_ => AssignableSessionOperatorAccessClient);
            services.RemoveAll<IAuthenticatedActorProfileAccessClient>();
            services.AddScoped<IAuthenticatedActorProfileAccessClient>(_ => AuthenticatedActorProfileAccessClient);
            services.RemoveAll<ITeamReferenceCatalogClient>();
            services.AddScoped<ITeamReferenceCatalogClient>(_ => TeamCatalogClient);
            services.RemoveAll<IMissionReadinessSource>();
            services.AddScoped<IMissionReadinessSource>(_ => MissionReadinessSource);
            services.RemoveAll<IMissionRuntimeSource>();
            services.AddScoped<IMissionRuntimeSource>(_ => MissionRuntimeSource);

            // Cut the real MassTransit publish path. appsettings points the broker at the compose
            // hostname "rabbitmq:5672", which doesn't resolve under the test host; MassTransit's
            // Publish then blocks awaiting a connection under its retry policy (it doesn't fail fast),
            // hanging every test that closes a question. A no-op endpoint keeps the handler's dispatch
            // synchronous and broker-free.
            services.RemoveAll<IPublishEndpoint>();
            services.AddSingleton<IPublishEndpoint, NoOpPublishEndpoint>();

            // Drop the MassTransit bus hosted service too, so it doesn't spam background connection
            // retries against the unreachable broker for the lifetime of the booted host.
            foreach (var busHostedServiceDescriptor in services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType?.Namespace?.StartsWith("MassTransit", StringComparison.Ordinal) == true)
                .ToList())
            {
                services.Remove(busHostedServiceDescriptor);
            }
        });
    }

    // No-op IPublishEndpoint: every publish/observer member is a no-op so factory-booted tests never
    // touch a broker (the real endpoint blocks on an unreachable RabbitMQ).
    private sealed class NoOpPublishEndpoint : IPublishEndpoint
    {
        public ConnectHandle ConnectPublishObserver(IPublishObserver observer) => new NoOpConnectHandle();

        public Task Publish<T>(T message, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task Publish(object message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<T>(object values, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        public Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken = default)
            where T : class => Task.CompletedTask;

        private sealed class NoOpConnectHandle : ConnectHandle
        {
            public void Disconnect()
            {
            }

            public void Dispose()
            {
            }
        }
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Deleting the aggregate root cascades to teams, participants, join contexts,
        // and team members (all FKs are ON DELETE CASCADE).
        await dbContext.LiveSessions.ExecuteDeleteAsync();
    }

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, null);
        base.Dispose(disposing);
    }
}
