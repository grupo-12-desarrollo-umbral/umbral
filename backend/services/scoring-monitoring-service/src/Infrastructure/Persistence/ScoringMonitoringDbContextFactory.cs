using Microsoft.EntityFrameworkCore.Design;

namespace umbral_backend.Infrastructure.Persistence;

public sealed class ScoringMonitoringDbContextFactory : IDesignTimeDbContextFactory<ScoringMonitoringDbContext>
{
    public ScoringMonitoringDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ScoringMonitoringDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("SCORING_MONITORING_SERVICE_CONNECTION_STRING")
            ?? "Host=localhost;Database=scoring_monitoring_service;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);

        return new ScoringMonitoringDbContext(optionsBuilder.Options);
    }
}
