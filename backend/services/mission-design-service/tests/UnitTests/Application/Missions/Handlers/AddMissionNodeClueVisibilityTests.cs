using umbral_backend.Application.Missions.Commands.AddMissionNode;
using umbral_backend.Application.UnitTests.Application.Missions.TestDoubles;
using umbral_backend.Domain.Entities;
using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

namespace umbral_backend.Application.UnitTests.Application.Missions.Handlers;

// Covers ParseClueVisibility's non-null arm (Enum.Parse) — the composite-shape test adds a clue
// without a visibility policy, exercising only the null default.
public sealed class AddMissionNodeClueVisibilityTests
{
    [Fact]
    public async Task AddClue_WithExplicitVisibilityPolicy_ParsesIt()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var handler = new AddMissionNodeCommandHandler(repository);

        await handler.Handle(new AddMissionNodeCommand(mission.Id, "Stage", "Stage", 1), CancellationToken.None);
        var stage = mission.Stages.Single();
        stage.Id = 10;
        await handler.Handle(
            new AddMissionNodeCommand(mission.Id, "Substage", "Substage", 1, stage.Id, PlayMode: "TreasureHunt"),
            CancellationToken.None);
        var substage = stage.Substages.Single();
        substage.Id = 20;

        var result = await handler.Handle(
            new AddMissionNodeCommand(
                mission.Id, "Clue", "Clue", 1, stage.Id, substage.Id,
                ClueText: "Look here.",
                ClueVisibilityPolicy: "VisibleWhenSubstageStarts"),
            CancellationToken.None);

        result.Stages!.Single().Substages!.Single().Clues!.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WithUnrecognizedNodeType_ThrowsValidationException()
    {
        var repository = new InMemoryMissionRepository();
        var mission = Mission.Create("Mission", "Briefing", "Advanced", 45);
        repository.Seed(mission);
        var handler = new AddMissionNodeCommandHandler(repository);

        var act = () => handler.Handle(
            new AddMissionNodeCommand(mission.Id, "Bogus", "Node", 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
