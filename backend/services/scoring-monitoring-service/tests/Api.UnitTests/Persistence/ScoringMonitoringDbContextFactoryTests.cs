using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.ScoringMonitoring.Api.UnitTests.Persistence;

public sealed class ScoringMonitoringDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WhenEnvVarIsSet_UsesEnvVarValue()
    {
        const string envVar = "SCORING_MONITORING_SERVICE_CONNECTION_STRING";
        var original = Environment.GetEnvironmentVariable(envVar);
        try
        {
            Environment.SetEnvironmentVariable(envVar, "Host=test-host;Database=test-db");

            var factory = new ScoringMonitoringDbContextFactory();
            var context = factory.CreateDbContext([]);

            context.Should().NotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, original);
        }
    }

    [Fact]
    public void CreateDbContext_WhenEnvVarIsNotSet_FallsBackToDefault()
    {
        const string envVar = "SCORING_MONITORING_SERVICE_CONNECTION_STRING";
        var original = Environment.GetEnvironmentVariable(envVar);
        try
        {
            Environment.SetEnvironmentVariable(envVar, null);

            var factory = new ScoringMonitoringDbContextFactory();
            var context = factory.CreateDbContext([]);

            context.Should().NotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, original);
        }
    }
}
