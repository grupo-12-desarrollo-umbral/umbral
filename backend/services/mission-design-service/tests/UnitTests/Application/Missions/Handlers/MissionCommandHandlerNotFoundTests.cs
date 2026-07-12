using umbral_backend.Application.Missions.Commands.AddMissionNode;
using umbral_backend.Application.Missions.Commands.AddTarget;
using umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;
using umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;
using umbral_backend.Application.Missions.Commands.RemoveMissionNode;
using umbral_backend.Application.Missions.Commands.RemoveTarget;
using umbral_backend.Application.Missions.Commands.SelectTriviaQuiz;
using umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;
using umbral_backend.Application.Missions.Commands.UpdateMissionNode;
using umbral_backend.Application.Missions.Commands.UpdateTarget;
using umbral_backend.Application.UnitTests.Application.Missions.TestDoubles;
using umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;
using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;

namespace umbral_backend.Application.UnitTests.Application.Missions.Handlers;

// Every mutating mission command loads the aggregate with `GetByIdAsync(...) ?? throw NotFound`.
// The happy-path handler tests seed the mission, so the not-found arm is only driven here: an empty
// repository makes GetByIdAsync return null for a missing mission id.
public sealed class MissionCommandHandlerNotFoundTests
{
    private const int MissingMissionId = 12345;

    private static async Task ShouldThrowNotFound(Func<Task> act) =>
        await act.Should().ThrowAsync<NotFoundException>();

    [Fact]
    public Task AddMissionNode_MissionMissing_ThrowsNotFound() =>
        ShouldThrowNotFound(() => new AddMissionNodeCommandHandler(new InMemoryMissionRepository())
            .Handle(new AddMissionNodeCommand(MissingMissionId, "Stage", "Stage", 1), CancellationToken.None));

    [Fact]
    public Task UpdateMissionNode_MissionMissing_ThrowsNotFound() =>
        ShouldThrowNotFound(() => new UpdateMissionNodeCommandHandler(new InMemoryMissionRepository())
            .Handle(new UpdateMissionNodeCommand(MissingMissionId, 1, "Title", 1), CancellationToken.None));

    [Fact]
    public Task RemoveMissionNode_MissionMissing_ThrowsNotFound() =>
        ShouldThrowNotFound(() => new RemoveMissionNodeCommandHandler(new InMemoryMissionRepository())
            .Handle(new RemoveMissionNodeCommand(MissingMissionId, 1), CancellationToken.None));

    [Fact]
    public Task AddTarget_MissionMissing_ThrowsNotFound() =>
        ShouldThrowNotFound(() => new AddTargetCommandHandler(new InMemoryMissionRepository())
            .Handle(new AddTargetCommand(MissingMissionId, 1, 2, "T", "QR", 1, 4.711, -74.0721), CancellationToken.None));

    [Fact]
    public Task UpdateTarget_MissionMissing_ThrowsNotFound() =>
        ShouldThrowNotFound(() => new UpdateTargetCommandHandler(new InMemoryMissionRepository())
            .Handle(new UpdateTargetCommand(MissingMissionId, 1, 2, 3, "T", "QR", 1, 4.711, -74.0721, true), CancellationToken.None));

    [Fact]
    public Task RemoveTarget_MissionMissing_ThrowsNotFound() =>
        ShouldThrowNotFound(() => new RemoveTargetCommandHandler(new InMemoryMissionRepository())
            .Handle(new RemoveTargetCommand(MissingMissionId, 1, 2, 3), CancellationToken.None));

    [Fact]
    public Task AssociateClueWithTarget_MissionMissing_ThrowsNotFound() =>
        ShouldThrowNotFound(() => new AssociateClueWithTargetCommandHandler(new InMemoryMissionRepository())
            .Handle(new AssociateClueWithTargetCommand(MissingMissionId, 1, 2, 3, 4), CancellationToken.None));

    [Fact]
    public Task UnassociateClueFromTarget_MissionMissing_ThrowsNotFound() =>
        ShouldThrowNotFound(() => new UnassociateClueFromTargetCommandHandler(new InMemoryMissionRepository())
            .Handle(new UnassociateClueFromTargetCommand(MissingMissionId, 1, 2, 3), CancellationToken.None));

    [Fact]
    public Task AssignSubstagePlayMode_MissionMissing_ThrowsNotFound() =>
        ShouldThrowNotFound(() => new AssignSubstagePlayModeCommandHandler(new InMemoryMissionRepository())
            .Handle(new AssignSubstagePlayModeCommand(MissingMissionId, 1, 2, "Trivia"), CancellationToken.None));

    [Fact]
    public Task SelectTriviaQuiz_MissionMissing_ThrowsNotFound() =>
        ShouldThrowNotFound(() => new SelectTriviaQuizCommandHandler(
                new InMemoryMissionRepository(), new InMemoryTriviaQuizRepository())
            .Handle(new SelectTriviaQuizCommand(MissingMissionId, 1, 2, 3), CancellationToken.None));
}
