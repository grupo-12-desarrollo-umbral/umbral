using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Missions.Commands.DeactivateMission;
using umbral_backend.Application.Missions.Commands.ActivateMission;
using umbral_backend.Application.Missions.Commands.AddMissionNode;
using umbral_backend.Application.Missions.Commands.AddTarget;
using umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;
using umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Commands.RemoveMissionNode;
using umbral_backend.Application.Missions.Commands.RemoveTarget;
using umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;
using umbral_backend.Application.Missions.Commands.UpdateMission;
using umbral_backend.Application.Missions.Commands.UpdateMissionNode;
using umbral_backend.Application.Missions.Commands.UpdateTarget;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionDetail;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;
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
