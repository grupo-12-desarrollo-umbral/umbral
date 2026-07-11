using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
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
                    notification.ActivatedAt == Now &&
                    notification.TriviaSubstageSnapshotId == session.ActiveSubstageId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ActivateNextQuestionAsync_WhenQuestionIsAlreadyActive_ReturnsWithoutPersistingOrBroadcasting()
    {
        var session = CreateTriviaSession();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-1), new SessionStateTransitionPolicy());
        session.MoveTo(SessionState.Active, Now, new SessionStateTransitionPolicy());
        session.ActivateQuestion(0, Now);
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var strategy = new Mock<IQuestionActivationStrategy>();
        var facade = new TriviaRoundOrchestratorFacade(
            repository.Object,
            broadcaster.Object,
            strategy.Object,
            new SessionStateTransitionPolicy());

        await facade.ActivateNextQuestionAsync(session, Now.AddSeconds(1), CancellationToken.None);

        repository.Verify(
            repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
        broadcaster.Verify(
            current => current.BroadcastQuestionActivatedAsync(
                It.IsAny<QuestionActivatedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        strategy.Verify(activationStrategy => activationStrategy.Next(It.IsAny<LiveSession>()), Times.Never);
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

    [Fact]
    public async Task CloseAndAdvanceAsync_OnLastQuestionOfSubstage_AdvancesToNextTriviaSubstageAndActivatesFirstQuestion()
    {
        var session = LiveSessionTestFactory.CreateScheduledMultiSubstageTrivia();
        ActivateFirstQuestion(session);
        var substages = OrderedSubstages(session);
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var facade = CreateFacadeWithRealStrategy(repository, broadcaster);

        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);

        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);
        session.ActiveQuestionIndex.Should().Be(0);
        session.State.Should().Be(SessionState.Active);
        broadcaster.Verify(
            current => current.BroadcastSubstageAdvancedAsync(
                It.Is<SubstageAdvancedNotificationDto>(notification =>
                    notification.LiveSessionId == session.LiveSessionId &&
                    notification.FromSubstageId == substages[0].SubstageSnapshotId &&
                    notification.FromPlayMode == "Trivia" &&
                    notification.ToSubstageId == substages[1].SubstageSnapshotId &&
                    notification.AdvancedAt == Now),
                It.IsAny<CancellationToken>()),
            Times.Once);
        broadcaster.Verify(
            current => current.BroadcastQuestionActivatedAsync(
                It.IsAny<QuestionActivatedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CloseAndAdvanceAsync_WhenNextSubstageIsTreasureHunt_ParksWithoutActivatingOrFinishing()
    {
        var session = LiveSessionTestFactory.CreateScheduledTriviaThenTreasureHunt();
        ActivateFirstQuestion(session);
        var substages = OrderedSubstages(session);
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var facade = CreateFacadeWithRealStrategy(repository, broadcaster);

        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);

        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);
        session.ActiveQuestionIndex.Should().BeNull();
        session.State.Should().Be(SessionState.Active);
        broadcaster.Verify(
            current => current.BroadcastSubstageAdvancedAsync(
                It.Is<SubstageAdvancedNotificationDto>(notification =>
                    notification.ToSubstageId == substages[1].SubstageSnapshotId &&
                    notification.FromPlayMode == "Trivia"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        broadcaster.Verify(
            current => current.BroadcastQuestionActivatedAsync(
                It.IsAny<QuestionActivatedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CloseAndAdvanceAsync_OnLastQuestionOfFinalSubstage_FinishesViaSessionCompletion()
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia(questionCount: 1);
        ActivateFirstQuestion(session);
        var substages = OrderedSubstages(session);
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var facade = CreateFacadeWithRealStrategy(repository, broadcaster);

        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);

        session.State.Should().Be(SessionState.Finished);
        session.ActiveQuestionIndex.Should().BeNull();
        // Finished is reached ONLY through the domain's substage-completion path — the flat-list
        // MoveTo(Finished) shortcut is gone: completion emits SubstageAdvancedEvent(to: null).
        session.DomainEvents
            .OfType<SubstageAdvancedEvent>()
            .Last()
            .ToSubstageId
            .Should()
            .BeNull();
        session.DomainEvents
            .OfType<SessionStateChangedEvent>()
            .Last()
            .CurrentState
            .Should()
            .Be(SessionState.Finished);
        broadcaster.Verify(
            current => current.BroadcastSubstageAdvancedAsync(
                It.Is<SubstageAdvancedNotificationDto>(notification =>
                    notification.FromSubstageId == substages[0].SubstageSnapshotId &&
                    notification.ToSubstageId == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CloseAndAdvanceAsync_WhenTickRepeatsAfterAdvance_DoesNotDoubleAdvanceOrRebroadcast()
    {
        var session = LiveSessionTestFactory.CreateScheduledTriviaThenTreasureHunt();
        ActivateFirstQuestion(session);
        var substages = OrderedSubstages(session);
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var facade = CreateFacadeWithRealStrategy(repository, broadcaster);

        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);
        await facade.CloseAndAdvanceAsync(session, Now.AddSeconds(1), CancellationToken.None);

        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);
        broadcaster.Verify(
            current => current.BroadcastSubstageAdvancedAsync(
                It.IsAny<SubstageAdvancedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        broadcaster.Verify(
            current => current.BroadcastQuestionClosedAsync(
                It.IsAny<QuestionClosedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TriviaRoundOrchestratorFacade CreateFacadeWithRealStrategy(
        Mock<ILiveSessionRepository> repository,
        Mock<ISessionQuestionBroadcaster> broadcaster)
    {
        return new TriviaRoundOrchestratorFacade(
            repository.Object,
            broadcaster.Object,
            new SequentialQuestionActivationStrategy(),
            new SessionStateTransitionPolicy());
    }

    private static void ActivateFirstQuestion(LiveSession session)
    {
        var policy = new SessionStateTransitionPolicy();
        session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, Now.AddMinutes(-1), policy);
        session.ActivateQuestion(0, Now.AddMinutes(-1));
    }

    private static SubstageSnapshot[] OrderedSubstages(LiveSession session)
    {
        return session.MissionRuntimeSnapshot.StageSnapshots
            .OrderBy(stage => stage.SequenceOrder)
            .SelectMany(stage => stage.SubstageSnapshots.OrderBy(substage => substage.SequenceOrder))
            .ToArray();
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
