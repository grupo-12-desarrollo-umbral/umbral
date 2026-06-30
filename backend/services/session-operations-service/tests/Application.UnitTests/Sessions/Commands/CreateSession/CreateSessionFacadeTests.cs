using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.CreateSession;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.CreateSession;

public sealed class CreateSessionFacadeTests
{
    private const int MissionId = 7;

    [Fact]
    public async Task CreateAsync_WhenMissionIsReady_CreatesScheduledSessionWithRuntimeSnapshot()
    {
        var command = CreateCommand();
        LiveSession? persistedSession = null;
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveSession, CancellationToken>((session, _) => persistedSession = session)
            .Returns(Task.CompletedTask);

        var facade = CreateFacade(
            repository,
            EligibleMissionSource(command.MissionId),
            RuntimeSource(command.MissionId, CreateMissionRuntime()));

        var result = await facade.CreateAsync(command, CancellationToken.None);

        result.Title.Should().Be(command.Title);
        result.SessionState.Should().Be(SessionState.Scheduled.ToString());
        result.SessionCode.Should().MatchRegex("^[A-Z0-9]{6}$");

        persistedSession.Should().NotBeNull();
        persistedSession!.Source.SourceType.Should().Be(SessionSourceType.Mission);
        persistedSession.Source.SourceEntityId.Should().NotBe(Guid.Empty);
        persistedSession.AssignedOperatorUserId.Should().BeNull();
        persistedSession.MissionRuntimeSnapshot.MissionTitle.Should().Be("Foundations of Science");
        persistedSession.MissionRuntimeSnapshot.MaximumTime.Minutes.Should().Be(45);
        persistedSession.MissionRuntimeSnapshot.StageSnapshots.Should().ContainSingle();
        persistedSession.MissionRuntimeSnapshot.TargetSnapshots.Should().ContainSingle();
        persistedSession.MissionRuntimeSnapshot.TriviaQuestionSnapshots.Should().ContainSingle();
        persistedSession.MissionRuntimeSnapshot.StageSnapshots.Single().Title.Should().Be("Stage One");
        persistedSession.MissionRuntimeSnapshot.StageSnapshots.Single().SubstageSnapshots.Should().HaveCount(2);

        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenMissionIsInactive_ThrowsNotEligibleAndDoesNotFetchRuntime()
    {
        var command = CreateCommand();
        var repository = new Mock<ILiveSessionRepository>();
        var runtimeSource = new Mock<IMissionRuntimeSource>();
        var facade = CreateFacade(
            repository,
            MissionSource(new MissionReadinessDto(command.MissionId, "Inactive", IsActive: false, IsReady: false, [])),
            runtimeSource);

        var act = async () => await facade.CreateAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<MissionNotEligibleForSessionCreationException>();
        runtimeSource.Verify(source => source.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenMissionIsNotReady_ThrowsNotEligibleAndDoesNotPersist()
    {
        var command = CreateCommand();
        var repository = new Mock<ILiveSessionRepository>();
        var facade = CreateFacade(
            repository,
            MissionSource(new MissionReadinessDto(command.MissionId, "Draft", IsActive: true, IsReady: false, ["No nodes"])),
            RuntimeSource(command.MissionId, CreateMissionRuntime()));

        var act = async () => await facade.CreateAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<MissionNotEligibleForSessionCreationException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenMissionReadinessIsMissing_ThrowsNotFoundException()
    {
        var command = CreateCommand();
        var repository = new Mock<ILiveSessionRepository>();
        var missionSource = new Mock<IMissionReadinessSource>();
        missionSource
            .Setup(source => source.GetByIdAsync(command.MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MissionReadinessDto?)null);

        var facade = CreateFacade(repository, missionSource, RuntimeSource(command.MissionId, CreateMissionRuntime()));

        var act = async () => await facade.CreateAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenMissionRuntimeIsMissing_ThrowsNotFoundException()
    {
        var command = CreateCommand();
        var repository = new Mock<ILiveSessionRepository>();
        var runtimeSource = new Mock<IMissionRuntimeSource>();
        runtimeSource
            .Setup(source => source.GetByIdAsync(command.MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MissionRuntimeDto?)null);

        var facade = CreateFacade(repository, EligibleMissionSource(command.MissionId), runtimeSource);

        var act = async () => await facade.CreateAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenTriviaSubstageResolvesEmpty_ThrowsAndDoesNotPersist()
    {
        var command = CreateCommand();
        var repository = new Mock<ILiveSessionRepository>();
        var facade = CreateFacade(
            repository,
            EligibleMissionSource(command.MissionId),
            RuntimeSource(command.MissionId, RuntimeWithEmptyTriviaSubstage()));

        var act = async () => await facade.CreateAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<TriviaSubstageSnapshotMustContainQuestionsException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CreateSessionCommand CreateCommand()
    {
        return new CreateSessionCommand(
            MissionId,
            "Mission Session",
            15,
            new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero));
    }

    private static CreateSessionFacade CreateFacade(
        Mock<ILiveSessionRepository> repository,
        Mock<IMissionReadinessSource> missionReadinessSource,
        Mock<IMissionRuntimeSource> missionRuntimeSource)
    {
        return new CreateSessionFacade(
            repository.Object,
            missionReadinessSource.Object,
            missionRuntimeSource.Object,
            new SessionCreationPolicy());
    }

    private static Mock<IMissionReadinessSource> EligibleMissionSource(int missionId)
    {
        return MissionSource(new MissionReadinessDto(missionId, "Ready", IsActive: true, IsReady: true, []));
    }

    private static Mock<IMissionReadinessSource> MissionSource(MissionReadinessDto readiness)
    {
        var source = new Mock<IMissionReadinessSource>();
        source
            .Setup(s => s.GetByIdAsync(readiness.MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readiness);
        return source;
    }

    private static Mock<IMissionRuntimeSource> RuntimeSource(int missionId, MissionRuntimeDto runtime)
    {
        var source = new Mock<IMissionRuntimeSource>();
        source
            .Setup(s => s.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runtime);
        return source;
    }

    private static MissionRuntimeDto CreateMissionRuntime()
    {
        return new MissionRuntimeDto(
            "Foundations of Science",
            45,
            [
                new MissionRuntimeStageDto(
                    "Stage One",
                    1,
                    [
                        new MissionRuntimeSubstageDto(
                            "Treasure Route",
                            1,
                            SubstagePlayMode.TreasureHunt.ToString(),
                            100,
                            [
                                new MissionRuntimeTargetDto(
                                    "Main Exhibit",
                                    "QR-001",
                                    1,
                                    true,
                                    new MissionRuntimeClueDto("Look near the entrance.", "VisibleAtStart"))
                            ],
                            []),
                        new MissionRuntimeSubstageDto(
                            "Trivia Round",
                            2,
                            SubstagePlayMode.Trivia.ToString(),
                            null,
                            [],
                            [
                                new MissionRuntimeTriviaQuestionDto(
                                    "What is the closest planet to the Sun?",
                                    1,
                                    100,
                                    30,
                                    null,
                                    [
                                        new MissionRuntimeTriviaOptionDto("Mercury", 1, true),
                                        new MissionRuntimeTriviaOptionDto("Venus", 2, false)
                                    ])
                            ])
                    ])
            ]);
    }

    // Mirrors what the mission runtime-plan returns when a selected quiz was archived after
    // readiness was evaluated: a trivia substage that resolves to zero questions.
    private static MissionRuntimeDto RuntimeWithEmptyTriviaSubstage()
    {
        return new MissionRuntimeDto(
            "Foundations of Science",
            45,
            [
                new MissionRuntimeStageDto(
                    "Stage One",
                    1,
                    [
                        new MissionRuntimeSubstageDto(
                            "Trivia Round",
                            1,
                            SubstagePlayMode.Trivia.ToString(),
                            null,
                            [],
                            [])
                    ])
            ]);
    }
}
