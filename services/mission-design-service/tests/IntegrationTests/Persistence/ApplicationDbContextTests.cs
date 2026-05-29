using Testcontainers.PostgreSql;
using umbral_backend.Domain.Entities;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Persistence;

public class ApplicationDbContextTests
{
    [Fact]
    public async Task CanPersistAndReadMissionAggregate()
    {
        await using var postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.Missions.Add(Mission.Create("Mission Beta", "Persisted through postgres", "Advanced", 60));
        await context.SaveChangesAsync(CancellationToken.None);

        var mission = await context.Missions.SingleAsync();

        mission.Name.Should().Be("Mission Beta");
        mission.Description.Should().Be("Persisted through postgres");
        mission.Difficulty.Value.Should().Be("Advanced");
        mission.MaximumTime.Minutes.Should().Be(60);
        mission.ActivationState.ToString().Should().Be("Draft");
    }
}
