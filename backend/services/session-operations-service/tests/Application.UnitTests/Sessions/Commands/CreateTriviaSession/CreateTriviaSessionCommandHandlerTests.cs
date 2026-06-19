using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.CreateTriviaSession;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Handlers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.CreateTriviaSession;

public sealed class CreateTriviaSessionCommandHandlerTests
{
    private const int MissionId = 7;

    [Fact]
    public async Task Handle_WhenQuizIsPublished_CreatesTriviaSessionWithFixedSnapshot()
    {
        var command = new CreateTriviaSessionCommand(
            MissionId,
            42,
            "Smoke Trivia",
            15,
            new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero));

        LiveSession? persistedSession = null;
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()))
            .Callback<LiveSession, CancellationToken>((session, _) => persistedSession = session)
            .Returns(Task.CompletedTask);

        var triviaQuizSource = new Mock<IPublishedTriviaQuizSource>();
        triviaQuizSource
            .Setup(source => source.GetByIdAsync(command.SourceTriviaQuizId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePublishedQuiz(command.SourceTriviaQuizId));

        var handler = CreateHandler(repository, triviaQuizSource, EligibleMissionSource(command.MissionId));

        var result = await handler.Handle(command, CancellationToken.None);

        result.Title.Should().Be(command.Title);
        result.SourceTriviaQuizId.Should().Be(command.SourceTriviaQuizId);
        result.SessionState.Should().Be(SessionState.Scheduled.ToString());
        result.SessionCode.Should().MatchRegex("^[A-Z0-9]{6}$");
        result.QuestionCount.Should().Be(1);

        persistedSession.Should().NotBeNull();
        persistedSession!.SessionMode.Should().Be(SessionMode.Trivia);
        persistedSession.SessionCode.Should().Be(result.SessionCode);
        persistedSession.SessionCode.Should().MatchRegex("^[A-Z0-9]{6}$");
        persistedSession.Source.SourceTriviaQuizId.Should().Be(command.SourceTriviaQuizId);
        persistedSession.Source.SourceType.Should().Be(SessionSourceType.TriviaQuiz);
        persistedSession.AssignedOperatorUserId.Should().BeNull();
        persistedSession.TriviaSnapshot.Should().NotBeNull();
        persistedSession.TriviaSnapshot!.QuizTitle.Should().Be("Quiz Night");
        persistedSession.TriviaSnapshot.Questions.Should().ContainSingle();
        persistedSession.TriviaSnapshot.Questions.Single().Options.Should().HaveCount(2);

        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenMissionIsInactive_ThrowsNotEligibleAndDoesNotPersist()
    {
        var command = CreateCommand();
        var repository = new Mock<ILiveSessionRepository>();
        var triviaQuizSource = new Mock<IPublishedTriviaQuizSource>();
        var missionSource = MissionSource(
            new MissionReadinessDto(command.MissionId, "Inactive", IsActive: false, IsReady: false, []));

        var handler = CreateHandler(repository, triviaQuizSource, missionSource);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<MissionNotEligibleForSessionCreationException>();
        triviaQuizSource.Verify(
            source => source.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMissionIsActiveButNotReady_ThrowsNotEligibleAndDoesNotPersist()
    {
        var command = CreateCommand();
        var repository = new Mock<ILiveSessionRepository>();
        var triviaQuizSource = new Mock<IPublishedTriviaQuizSource>();
        var missionSource = MissionSource(
            new MissionReadinessDto(command.MissionId, "Draft", IsActive: true, IsReady: false, ["No nodes"]));

        var handler = CreateHandler(repository, triviaQuizSource, missionSource);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<MissionNotEligibleForSessionCreationException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMissionIsMissing_ThrowsNotFoundException()
    {
        var command = CreateCommand();
        var repository = new Mock<ILiveSessionRepository>();
        var triviaQuizSource = new Mock<IPublishedTriviaQuizSource>();
        var missionSource = new Mock<IMissionReadinessSource>();
        missionSource
            .Setup(source => source.GetByIdAsync(command.MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MissionReadinessDto?)null);

        var handler = CreateHandler(repository, triviaQuizSource, missionSource);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenQuizIsDraft_ThrowsNotPublishedException()
    {
        var command = CreateCommand();
        var repository = new Mock<ILiveSessionRepository>();
        var triviaQuizSource = new Mock<IPublishedTriviaQuizSource>();
        triviaQuizSource
            .Setup(source => source.GetByIdAsync(command.SourceTriviaQuizId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateQuiz(command.SourceTriviaQuizId, "Draft"));

        var handler = CreateHandler(repository, triviaQuizSource, EligibleMissionSource(command.MissionId));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<SourceTriviaQuizNotPublishedException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenQuizIsArchived_ThrowsNotPublishedException()
    {
        var command = CreateCommand();
        var repository = new Mock<ILiveSessionRepository>();
        var triviaQuizSource = new Mock<IPublishedTriviaQuizSource>();
        triviaQuizSource
            .Setup(source => source.GetByIdAsync(command.SourceTriviaQuizId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateQuiz(command.SourceTriviaQuizId, "Archived"));

        var handler = CreateHandler(repository, triviaQuizSource, EligibleMissionSource(command.MissionId));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<SourceTriviaQuizNotPublishedException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenQuizIsMissing_ThrowsNotFoundException()
    {
        var command = CreateCommand();
        var repository = new Mock<ILiveSessionRepository>();
        var triviaQuizSource = new Mock<IPublishedTriviaQuizSource>();
        triviaQuizSource
            .Setup(source => source.GetByIdAsync(command.SourceTriviaQuizId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublishedTriviaQuizDto?)null);

        var handler = CreateHandler(repository, triviaQuizSource, EligibleMissionSource(command.MissionId));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CreateTriviaSessionCommand CreateCommand()
    {
        return new CreateTriviaSessionCommand(
            MissionId,
            42,
            "Smoke Trivia",
            15,
            new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero));
    }

    private static CreateTriviaSessionCommandHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        Mock<IPublishedTriviaQuizSource> triviaQuizSource,
        Mock<IMissionReadinessSource> missionReadinessSource)
    {
        var facade = new CreateTriviaSessionFacade(
            repository.Object,
            triviaQuizSource.Object,
            missionReadinessSource.Object,
            new SessionCreationPolicy());
        return new CreateTriviaSessionCommandHandler(facade);
    }

    private static Mock<IMissionReadinessSource> EligibleMissionSource(int missionId)
    {
        return MissionSource(
            new MissionReadinessDto(missionId, "Ready", IsActive: true, IsReady: true, []));
    }

    private static Mock<IMissionReadinessSource> MissionSource(MissionReadinessDto readiness)
    {
        var source = new Mock<IMissionReadinessSource>();
        source
            .Setup(s => s.GetByIdAsync(readiness.MissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readiness);
        return source;
    }

    private static PublishedTriviaQuizDto CreatePublishedQuiz(int triviaQuizId)
    {
        return CreateQuiz(triviaQuizId, "Published");
    }

    private static PublishedTriviaQuizDto CreateQuiz(int triviaQuizId, string status)
    {
        return new PublishedTriviaQuizDto(
            triviaQuizId,
            "Quiz Night",
            status,
            [
                new PublishedTriviaQuestionDto(
                    7,
                    "Capital of France?",
                    2,
                    true,
                    50,
                    30,
                    "Paris is the capital city.",
                    [
                        new PublishedTriviaOptionDto(1, "Paris", 2, true),
                        new PublishedTriviaOptionDto(2, "Lyon", 1, false)
                    ]),
                new PublishedTriviaQuestionDto(
                    8,
                    "Ignored inactive question",
                    1,
                    false,
                    25,
                    20,
                    null,
                    [
                        new PublishedTriviaOptionDto(3, "A", 1, true),
                        new PublishedTriviaOptionDto(4, "B", 2, false)
                    ])
            ]);
    }
}
