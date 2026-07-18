using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Missions.Commands.UpdateMission;
using umbral_backend.Application.Missions.Commands.ActivateMission;
using umbral_backend.Application.Missions.Commands.AddMissionNode;
using umbral_backend.Application.Missions.Commands.AddTarget;
using umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;
using umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Commands.DeactivateMission;
using umbral_backend.Application.Missions.Commands.RemoveMissionNode;
using umbral_backend.Application.Missions.Commands.RemoveTarget;
using umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;
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

public sealed class UpdateMissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenMissionExists_UpdatesMissionAndReturnsDetail()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission One", "Briefing", "Advanced", 30);
        repository.Seed(mission);
        var handler = new UpdateMissionCommandHandler(repository);

        var result = await handler.Handle(
            new UpdateMissionCommand(mission.Id, "Mission Two", "Updated", "Beginner", 30),
            CancellationToken.None);

        repository.LastUpdatedMission.Should().BeSameAs(mission);
        result.Id.Should().Be(mission.Id);
        result.Name.Should().Be("Mission Two");
        result.Description.Should().Be("Updated");
        result.Difficulty.Should().Be("Beginner");
        result.MaximumTimeMinutes.Should().Be(30);
        result.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Handle_WhenMissionDoesNotExist_ThrowsNotFound()
    {
        var repository = new InMemoryMissionRepository();
        var handler = new UpdateMissionCommandHandler(repository);

        var act = () => handler.Handle(
            new UpdateMissionCommand(99, "Mission", "Briefing", "Advanced", 30),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"Mission\" (99) was not found.");
    }

    [Fact]
    public async Task Handle_WhenMissionIsInactive_ThrowsAndDoesNotPersist()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission One", "Briefing", "Advanced", 30);
        mission.Deactivate(new DateTimeOffset(2026, 5, 31, 13, 0, 0, TimeSpan.Zero));
        repository.Seed(mission);
        var handler = new UpdateMissionCommandHandler(repository);

        var act = () => handler.Handle(
            new UpdateMissionCommand(mission.Id, "Mission Two", "Updated", "Beginner", 30),
            CancellationToken.None);

        await act.Should().ThrowAsync<MissionNotEditableWhileInactiveException>();
        repository.LastUpdatedMission.Should().BeNull();
        mission.Name.Should().Be("Mission One");
    }
}
