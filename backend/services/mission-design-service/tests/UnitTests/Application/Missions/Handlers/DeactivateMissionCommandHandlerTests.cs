using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Missions.Commands.DeactivateMission;
using umbral_backend.Application.Missions.Handlers;
using umbral_backend.Application.UnitTests.Application.Missions.TestDoubles;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Application.Missions.Handlers;

public sealed class DeactivateMissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenMissionExists_DeactivatesMission()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var archivedAt = new DateTimeOffset(2026, 5, 31, 14, 0, 0, TimeSpan.Zero);
        var handler = new DeactivateMissionCommandHandler(repository, new StubClock(archivedAt));

        await handler.Handle(new DeactivateMissionCommand(mission.Id), CancellationToken.None);

        repository.LastUpdatedMission.Should().BeSameAs(mission);
        mission.IsActive.Should().BeFalse();
        mission.ArchivedAt.Should().Be(archivedAt);
        mission.ActivationState.ToString().Should().Be("Inactive");
    }

    [Fact]
    public async Task Handle_WhenMissionDoesNotExist_ThrowsNotFound()
    {
        var repository = new InMemoryMissionRepository();
        var handler = new DeactivateMissionCommandHandler(
            repository,
            new StubClock(new DateTimeOffset(2026, 5, 31, 14, 0, 0, TimeSpan.Zero)));

        var act = () => handler.Handle(new DeactivateMissionCommand(99), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"Mission\" (99) was not found.");
    }

    [Fact]
    public async Task Handle_WhenMissionIsAlreadyInactive_ThrowsDomainException()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        mission.Deactivate(new DateTimeOffset(2026, 5, 31, 13, 0, 0, TimeSpan.Zero));
        repository.Seed(mission);
        var handler = new DeactivateMissionCommandHandler(
            repository,
            new StubClock(new DateTimeOffset(2026, 5, 31, 14, 0, 0, TimeSpan.Zero)));

        var act = () => handler.Handle(new DeactivateMissionCommand(mission.Id), CancellationToken.None);

        await act.Should().ThrowAsync<MissionAlreadyDeactivatedException>();
    }
}
