using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Persistence;

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

    public FakePublishedTriviaQuizSource TriviaQuizSource { get; } = new();

    public FakeMissionReadinessSource MissionReadinessSource { get; } = new();

    public FakeAuthenticatedActorProfileAccessClient AuthenticatedActorProfileAccessClient { get; } = new();

    public FakeTeamReferenceCatalogClient TeamCatalogClient { get; } = new();

    public FakeSessionTeamAssociationSyncClient SessionTeamAssociationSyncClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IParticipantMembershipAccessClient>();
            services.AddScoped<IParticipantMembershipAccessClient>(_ => AccessClient);
            services.RemoveAll<IAssignableSessionOperatorAccessClient>();
            services.AddScoped<IAssignableSessionOperatorAccessClient>(_ => AssignableSessionOperatorAccessClient);
            services.RemoveAll<IAuthenticatedActorProfileAccessClient>();
            services.AddScoped<IAuthenticatedActorProfileAccessClient>(_ => AuthenticatedActorProfileAccessClient);
            services.RemoveAll<ITeamReferenceCatalogClient>();
            services.AddScoped<ITeamReferenceCatalogClient>(_ => TeamCatalogClient);
            services.RemoveAll<ISessionTeamAssociationSyncClient>();
            services.AddScoped<ISessionTeamAssociationSyncClient>(_ => SessionTeamAssociationSyncClient);
            services.RemoveAll<IPublishedTriviaQuizSource>();
            services.AddScoped<IPublishedTriviaQuizSource>(_ => TriviaQuizSource);
            services.RemoveAll<IMissionReadinessSource>();
            services.AddScoped<IMissionReadinessSource>(_ => MissionReadinessSource);
        });
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
