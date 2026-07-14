using Npgsql;
using Testcontainers.PostgreSql;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.ScoringMonitoring.IntegrationTests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await DockerAvailability.StartOrSkipAsync(() => _postgres.StartAsync(), _postgres.DisposeAsync);

        var options = new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var context = new ScoringMonitoringDbContext(options);
                await context.Database.MigrateAsync();
                return;
            }
            catch (Exception exception) when (attempt < maxAttempts && exception is NpgsqlException or TimeoutException)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        await using var finalContext = new ScoringMonitoringDbContext(options);
        await finalContext.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        return _postgres.DisposeAsync().AsTask();
    }
}
