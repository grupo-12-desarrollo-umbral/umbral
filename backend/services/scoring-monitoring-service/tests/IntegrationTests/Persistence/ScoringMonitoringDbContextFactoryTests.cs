using umbral_backend.Infrastructure.Identity;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

public sealed class ScoringMonitoringDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_UsesTheConfiguredEnvironmentConnectionString()
    {
        const string key = "SCORING_MONITORING_SERVICE_CONNECTION_STRING";
        const string connectionString = "Host=test-host;Database=test-db;Username=test-user;Password=test-pass";
        var previous = Environment.GetEnvironmentVariable(key);

        try
        {
            Environment.SetEnvironmentVariable(key, connectionString);

            using var context = new ScoringMonitoringDbContextFactory().CreateDbContext([]);

            context.Database.GetConnectionString().Should().Be(connectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
        }
    }

    [Fact]
    public void SessionOperationsClientOptions_ExposeTheExpectedDefaults()
    {
        SessionOperationsClientOptions.SectionName.Should().Be("SessionOperations");
        new SessionOperationsClientOptions().BaseAddress.Should().Be("http://session-operations-service:8080");
    }
}
