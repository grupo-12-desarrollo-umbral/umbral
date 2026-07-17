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
        var facade = CreateFacade(repository, broadcaster, strategy);

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
        var facade = CreateFacade(repository, broadcaster, strategy);

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
    public async Task CloseAndAdvanceAsync_OnNonLastQuestion_BroadcastsClosureAndBeginsRevealWithoutActivatingNext()
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
        var facade = CreateFacade(repository, broadcaster, strategy);

        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);

        // Close only opens the reveal window — the next question is NOT activated yet (HU-35 dwell).
        session.ActiveQuestionIndex.Should().BeNull();
        session.IsAwaitingQuestionReveal.Should().BeTrue();
        session.QuestionRevealUntil.Should().Be(Now + TriviaRoundOrchestratorFacade.QuestionRevealDuration);
        repository.Verify(
            repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()),
            Times.Once);
        broadcaster.Verify(
            current => current.BroadcastQuestionClosedAsync(
                It.Is<QuestionClosedNotificationDto>(notification =>
                    notification.LiveSessionId == session.LiveSessionId &&
                    notification.QuestionIndex == 0 &&
                    notification.ClosedAt == Now &&
                    notification.WasExpiredByTimer &&
                    notification.CorrectOptionSequenceOrder == 1 &&
                    notification.Explanation == "Mercury is the closest planet."),
                It.IsAny<CancellationToken>()),
            Times.Once);
        broadcaster.Verify(
            current => current.BroadcastQuestionActivatedAsync(
                It.IsAny<QuestionActivatedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CompleteQuestionRevealAsync_OnNonLastQuestion_ActivatesNextQuestionAndEndsReveal()
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
        var facade = CreateFacade(repository, broadcaster, strategy);

        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);
        await facade.CompleteQuestionRevealAsync(
            session,
            Now + TriviaRoundOrchestratorFacade.QuestionRevealDuration,
            CancellationToken.None);

        session.ActiveQuestionIndex.Should().Be(1);
        session.IsAwaitingQuestionReveal.Should().BeFalse();
        broadcaster.Verify(
            current => current.BroadcastQuestionActivatedAsync(
                It.Is<QuestionActivatedNotificationDto>(notification => notification.QuestionIndex == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompleteQuestionRevealAsync_OnLastQuestion_BeginsRankingRevealInsteadOfFinishing()
    {
        // D-3 changed this flow: the last question's answer reveal used to advance/finish immediately.
        // It now hands off to the coordinator for the 10s ranking, and the session stays Active until
        // that window elapses. The facade must NOT finish the session itself any more.
        var session = CreateTriviaSession(questionCount: 1);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, Now.AddMinutes(-1), policy);
        session.ActivateQuestion(0, Now.AddMinutes(-1));
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var strategy = new Mock<IQuestionActivationStrategy>();
        strategy.Setup(activationStrategy => activationStrategy.Next(session)).Returns((int?)null);
        var coordinator = new Mock<ISubstageAdvanceCoordinator>();
        var facade = CreateFacade(repository, broadcaster, strategy, coordinator.Object);

        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);

        // The close alone does NOT finish the session — it holds the answer reveal.
        session.State.Should().Be(SessionState.Active);
        session.IsAwaitingQuestionReveal.Should().BeTrue();

        await facade.CompleteQuestionRevealAsync(session, Now.AddSeconds(5), CancellationToken.None);

        session.ActiveQuestionIndex.Should().BeNull();
        session.State.Should().Be(SessionState.Active);
        coordinator.Verify(
            current => current.BeginRankingRevealAsync(
                session,
                Now.AddSeconds(5),
                It.IsAny<CancellationToken>()),
            Times.Once);
        broadcaster.Verify(
            current => current.BroadcastQuestionClosedAsync(
                It.IsAny<QuestionClosedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompleteQuestionRevealAsync_OnLastQuestionOfSubstage_ShowsRankingThenAdvancesToNextTriviaSubstage()
    {
        var session = LiveSessionTestFactory.CreateScheduledMultiSubstageTrivia();
        ActivateFirstQuestion(session);
        var substages = OrderedSubstages(session);
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var (facade, coordinator) = CreateFacadeWithRealStrategy(repository, broadcaster);

        // The full D-3 sequence: close -> 5s answer reveal -> 10s ranking reveal -> advance.
        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);
        session.IsAwaitingQuestionReveal.Should().BeTrue();
        await facade.CompleteQuestionRevealAsync(session, Now.AddSeconds(5), CancellationToken.None);

        // The substage has NOT advanced yet — the ranking is on screen and the pointer has not moved.
        session.IsAwaitingSubstageRankingReveal.Should().BeTrue();
        session.ActiveSubstageId.Should().Be(substages[0].SubstageSnapshotId);

        var advancedAt = Now.AddSeconds(5) + LiveSession.SubstageRankingRevealDuration;
        await coordinator.CompleteRankingRevealAsync(session, advancedAt, CancellationToken.None);

        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);
        session.ActiveQuestionIndex.Should().Be(0);
        session.State.Should().Be(SessionState.Active);
        session.IsAwaitingSubstageRankingReveal.Should().BeFalse();
        broadcaster.Verify(
            current => current.BroadcastSubstageAdvancedAsync(
                It.Is<SubstageAdvancedNotificationDto>(notification =>
                    notification.LiveSessionId == session.LiveSessionId &&
                    notification.FromSubstageId == substages[0].SubstageSnapshotId &&
                    notification.FromPlayMode == "Trivia" &&
                    notification.ToSubstageId == substages[1].SubstageSnapshotId &&
                    notification.AdvancedAt == advancedAt),
                It.IsAny<CancellationToken>()),
            Times.Once);
        broadcaster.Verify(
            current => current.BroadcastQuestionActivatedAsync(
                It.IsAny<QuestionActivatedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompleteQuestionRevealAsync_WhenNextSubstageIsTreasureHunt_AdvancesIntoItWithoutActivatingAQuestion()
    {
        var session = LiveSessionTestFactory.CreateScheduledTriviaThenTreasureHunt();
        ActivateFirstQuestion(session);
        var substages = OrderedSubstages(session);
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var (facade, coordinator) = CreateFacadeWithRealStrategy(repository, broadcaster);

        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);
        await facade.CompleteQuestionRevealAsync(session, Now.AddSeconds(5), CancellationToken.None);
        await coordinator.CompleteRankingRevealAsync(session, Now.AddSeconds(15), CancellationToken.None);

        // The hunt is now the live substage with no question activated — it ends when a team clears it
        // (D-1), not on a timer. It no longer "parks": the pointer is movable from here.
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
    public async Task CompleteQuestionRevealAsync_OnLastQuestionOfFinalSubstage_FinishesViaSessionCompletion()
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia(questionCount: 1);
        ActivateFirstQuestion(session);
        var substages = OrderedSubstages(session);
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var (facade, coordinator) = CreateFacadeWithRealStrategy(repository, broadcaster);

        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);
        await facade.CompleteQuestionRevealAsync(session, Now.AddSeconds(5), CancellationToken.None);

        // Last substage: the ranking is terminal — the session finishes ON it, not before it (D-3).
        session.IsAwaitingSubstageRankingReveal.Should().BeTrue();
        session.State.Should().Be(SessionState.Active);

        await coordinator.CompleteRankingRevealAsync(session, Now.AddSeconds(15), CancellationToken.None);

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
    public async Task CloseAndAdvanceAsync_WhenTickRepeatsDuringReveal_DoesNotReCloseOrAdvanceEarly()
    {
        var session = LiveSessionTestFactory.CreateScheduledTriviaThenTreasureHunt();
        ActivateFirstQuestion(session);
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var (facade, coordinator) = CreateFacadeWithRealStrategy(repository, broadcaster);

        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);
        // A repeat tick while the reveal is still open (ActiveQuestionIndex is null) is a no-op: the
        // close guard returns early, so no second closure and no early advance.
        await facade.CloseAndAdvanceAsync(session, Now.AddSeconds(1), CancellationToken.None);

        session.IsAwaitingQuestionReveal.Should().BeTrue();
        broadcaster.Verify(
            current => current.BroadcastQuestionClosedAsync(
                It.IsAny<QuestionClosedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        broadcaster.Verify(
            current => current.BroadcastSubstageAdvancedAsync(
                It.IsAny<SubstageAdvancedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CompleteQuestionRevealAsync_WhenTickRepeatsAfterReveal_DoesNotDoubleAdvanceOrRebroadcast()
    {
        var session = LiveSessionTestFactory.CreateScheduledTriviaThenTreasureHunt();
        ActivateFirstQuestion(session);
        var substages = OrderedSubstages(session);
        var repository = CreateRepository();
        var broadcaster = new Mock<ISessionQuestionBroadcaster>();
        var (facade, coordinator) = CreateFacadeWithRealStrategy(repository, broadcaster);

        await facade.CloseAndAdvanceAsync(session, Now, CancellationToken.None);
        await facade.CompleteQuestionRevealAsync(session, Now.AddSeconds(5), CancellationToken.None);
        // A duplicate reveal-completion tick is guarded by IsAwaitingQuestionReveal, so it must not
        // open a second ranking reveal on top of the one already running.
        await facade.CompleteQuestionRevealAsync(session, Now.AddSeconds(6), CancellationToken.None);
        session.SubstageRevealUntil.Should().Be(Now.AddSeconds(5) + LiveSession.SubstageRankingRevealDuration);

        await coordinator.CompleteRankingRevealAsync(session, Now.AddSeconds(15), CancellationToken.None);
        // ...and a duplicate ranking-reveal tick must not advance twice.
        await coordinator.CompleteRankingRevealAsync(session, Now.AddSeconds(16), CancellationToken.None);

        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);
        session.IsAwaitingQuestionReveal.Should().BeFalse();
        session.IsAwaitingSubstageRankingReveal.Should().BeFalse();
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

    // Real strategy, real activator, real coordinator: these tests drive the whole trivia chain
    // (close -> 5s answer reveal -> 10s ranking reveal -> advance), so stubbing the coordinator would
    // hide the seam the facade now hands off across.
    private static (TriviaRoundOrchestratorFacade Facade, ISubstageAdvanceCoordinator Coordinator) CreateFacadeWithRealStrategy(
        Mock<ILiveSessionRepository> repository,
        Mock<ISessionQuestionBroadcaster> broadcaster)
    {
        var activator = new QuestionActivator(
            repository.Object,
            broadcaster.Object,
            new SequentialQuestionActivationStrategy());
        var coordinator = new SubstageAdvanceCoordinator(
            repository.Object,
            broadcaster.Object,
            activator,
            new SessionStateTransitionPolicy());
        var facade = new TriviaRoundOrchestratorFacade(
            repository.Object,
            broadcaster.Object,
            new SequentialQuestionActivationStrategy(),
            activator,
            coordinator);

        return (facade, coordinator);
    }

    private static TriviaRoundOrchestratorFacade CreateFacade(
        Mock<ILiveSessionRepository> repository,
        Mock<ISessionQuestionBroadcaster> broadcaster,
        Mock<IQuestionActivationStrategy> strategy,
        ISubstageAdvanceCoordinator? coordinator = null)
    {
        return new TriviaRoundOrchestratorFacade(
            repository.Object,
            broadcaster.Object,
            strategy.Object,
            new QuestionActivator(repository.Object, broadcaster.Object, strategy.Object),
            coordinator ?? Mock.Of<ISubstageAdvanceCoordinator>());
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
