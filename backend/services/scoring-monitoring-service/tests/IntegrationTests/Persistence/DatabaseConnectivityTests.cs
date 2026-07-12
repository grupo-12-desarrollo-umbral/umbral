using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

// Exercises the real Npgsql-backed ApplicationDbContext and DatabaseHealthCheck against the shared
// Postgres Testcontainer (ADR-0008): the empty InitScoringMonitoring migration has been applied by
// the fixture, so the context connects and reports healthy.
[Collection(PostgreSqlCollection.Name)]
public sealed class DatabaseConnectivityTests
{
    private readonly PostgreSqlFixture _fixture;

    public DatabaseConnectivityTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private ApplicationDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task DatabaseHealthCheck_AgainstMigratedDatabase_ReportsHealthy()
    {
        await using var context = NewContext();
        var healthCheck = new DatabaseHealthCheck(context);

        var canConnect = await healthCheck.CanConnectAsync(CancellationToken.None);

        canConnect.Should().BeTrue();
    }

    [Fact]
    public async Task MigrationsHistory_ContainsInitScoringMonitoring()
    {
        await using var context = NewContext();

        var applied = await context.Database.GetAppliedMigrationsAsync();

        applied.Should().Contain(migration => migration.EndsWith("InitScoringMonitoring"));
    }
}
