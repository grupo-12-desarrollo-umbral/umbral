using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

// Exercises the real Npgsql-backed ScoringMonitoringDbContext and DatabaseHealthCheck against the
// shared Postgres Testcontainer (ADR-0008): the fixture has applied this context's migrations, so
// the context connects and reports healthy.
[Collection(PostgreSqlCollection.Name)]
public sealed class DatabaseConnectivityTests
{
    private readonly PostgreSqlFixture _fixture;

    public DatabaseConnectivityTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private ScoringMonitoringDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new ScoringMonitoringDbContext(options);
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
    public async Task MigrationsHistory_ContainsScoringLedgerAndRanking()
    {
        await using var context = NewContext();

        var applied = await context.Database.GetAppliedMigrationsAsync();

        applied.Should().Contain(migration => migration.EndsWith("AddScoringLedgerAndRanking"));
    }
}
