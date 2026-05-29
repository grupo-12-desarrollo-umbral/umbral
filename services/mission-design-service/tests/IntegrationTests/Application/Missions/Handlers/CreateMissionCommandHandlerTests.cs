using Testcontainers.PostgreSql;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Handlers;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Application.Missions.Handlers;

public class CreateMissionCommandHandlerTests
{
    [Fact]
    public async Task HandlePersistsMissionInDraftState()
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

        var handler = new CreateMissionCommandHandler(context);

        var command = new CreateMissionCommand("Test Mission", "Test Description", "Advanced", 45);
        var result = await handler.Handle(command, CancellationToken.None);

        var mission = await context.Missions.SingleAsync();
        mission.ActivationState.ToString().Should().Be("Draft");
        result.Status.Should().Be("Draft");
    }
}
