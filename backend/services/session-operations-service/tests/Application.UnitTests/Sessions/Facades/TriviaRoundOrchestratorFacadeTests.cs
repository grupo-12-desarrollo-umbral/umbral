using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Facades;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Facades;

public sealed class TriviaRoundOrchestratorFacadeTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ActivateNextQuestionAsync_PersistsAndBroadcastsQuestionActivated()
    {
        var session = CreateTriviaSession();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-1), new SessionStateTransitionPolicy());
        session.MoveTo(SessionState.Active, Now, new SessionStateTransitionPolicy());
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var strategy = new Mock<IQuestionActivationStrategy>();
        strategy.Setup(activationStrategy => activationStrategy.Next(session)).Returns(0);
        var facade = new TriviaRoundOrchestratorFacade(
            repository.Object,
            broadcaster.Object,
            strategy.Object,
            new SessionStateTransitionPolicy());

        await facade.ActivateNextQuestionAsync(session, Now, CancellationToken.None);

        session.ActiveQuestionIndex.Should().Be(0);
        repository.Verify(
            repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
        broadcaster.Verify(
            current => current.BroadcastQuestionActivatedAsync(
                It.Is<QuestionActivatedNotificationDto>(notification =>
                    notification.LiveSessionId == session.LiveSessionId &&
                    notification.QuestionIndex == 0 &&
                    notification.SequenceOrder == 1 &&
                    notification.Prompt == "What is the closest planet to the Sun?" &&
                    notification.Options.SequenceEqual(new[] { "Mercury", "Venus" }) &&
                    notification.TimeLimitSeconds == 30 &&
                    notification.ActivatedAt == Now),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CloseAndAdvanceAsync_OnNonLastQuestion_BroadcastsClosureThenActivatesNextQuestion()
    {
        var session = CreateTriviaSession();
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, Now.AddMinutes(-1), policy);
        session.ActivateQuestion(0, Now.AddMinutes(-1));
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var strategy = new Mock<IQuestionActivationStrategy>();
        strategy.Setup(activationStrategy => activationStrategy.Next(session)).Returns(1);
        var facade = new TriviaRoundOrchestratorFacade(
            repository.Object,
            broadcaster.Object,
            strategy.Object,
            policy);

        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);

        session.ActiveQuestionIndex.Should().Be(1);
        repository.Verify(
            repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        broadcaster.Verify(
            current => current.BroadcastQuestionClosedAsync(
                It.Is<QuestionClosedNotificationDto>(notification =>
                    notification.LiveSessionId == session.LiveSessionId &&
                    notification.QuestionIndex == 0 &&
                    notification.ClosedAt == Now &&
                    notification.WasExpiredByTimer),
                It.IsAny<CancellationToken>()),
            Times.Once);
        broadcaster.Verify(
            current => current.BroadcastQuestionActivatedAsync(
                It.Is<QuestionActivatedNotificationDto>(notification => notification.QuestionIndex == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CloseAndAdvanceAsync_OnLastQuestion_TransitionsSessionToFinished()
    {
        var session = CreateTriviaSession(questionCount: 1);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, Now.AddMinutes(-1), policy);
        session.ActivateQuestion(0, Now.AddMinutes(-1));
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var strategy = new Mock<IQuestionActivationStrategy>();
        strategy.Setup(activationStrategy => activationStrategy.Next(session)).Returns((int?)null);
        var facade = new TriviaRoundOrchestratorFacade(
            repository.Object,
            broadcaster.Object,
            strategy.Object,
            policy);

        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);

        session.ActiveQuestionIndex.Should().BeNull();
        session.State.Should().Be(SessionState.Finished);
        repository.Verify(
            repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        broadcaster.Verify(
            current => current.BroadcastQuestionClosedAsync(
                It.IsAny<QuestionClosedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        session.DomainEvents
            .OfType<SessionStateChangedEvent>()
            .Last()
            .CurrentState
            .Should()
            .Be(SessionState.Finished);
    }

    private static Mock<ILiveSessionRepository> CreateRepository()
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repository;
    }

    private static LiveSession CreateTriviaSession(int questionCount = 3)
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia(
            $"TRV-{Guid.NewGuid():N}"[..12],
            "Trivia Session",
            45,
            questionCount,
            Now.AddHours(1));

        session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        return session;
    }
}
