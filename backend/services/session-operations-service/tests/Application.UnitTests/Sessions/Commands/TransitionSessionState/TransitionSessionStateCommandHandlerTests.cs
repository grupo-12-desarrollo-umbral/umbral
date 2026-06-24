using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;
using umbral_backend.Application.Sessions.Commands.TransitionSessionState;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.StateTransitions;
using umbral_backend.Application.Sessions.StateTransitions.Validators;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.TransitionSessionState;

public sealed class TransitionSessionStateCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WithAllowedTransition_MovesStateAndRaisesEvent()
    {
        var session = CreateScheduledSession(assignedOperatorUserId: 42, registerTeam: true);
        var repository = CreateRepository(session);
        var handler = CreateHandler(repository, CreateCurrentUser("kc-operator-42", "Operator"), resolvedUserId: 42);
        var command = new TransitionSessionStateCommand(session.LiveSessionId, SessionState.Preparing, "Doors open");

        var result = await handler.Handle(command, CancellationToken.None);

        result.PreviousState.Should().Be(nameof(SessionState.Scheduled));
        result.CurrentState.Should().Be(nameof(SessionState.Preparing));
        session.State.Should().Be(SessionState.Preparing);
        session.DomainEvents.OfType<SessionStateChangedEvent>()
            .Last().CurrentState.Should().Be(SessionState.Preparing);
        repository.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPausingActiveSession_ReturnsFrozenTimerSnapshot()
    {
        var session = CreateScheduledSession(assignedOperatorUserId: 42, registerTeam: true);
        var transitionPolicy = new SessionStateTransitionPolicy();
        var activeAt = Now.AddMinutes(-9);
        session.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), transitionPolicy);
        session.MoveTo(SessionState.Active, activeAt, transitionPolicy);
        var repository = CreateRepository(session);
        var handler = CreateHandler(repository, CreateCurrentUser("kc-operator-42", "Operator"), resolvedUserId: 42);
        var command = new TransitionSessionStateCommand(session.LiveSessionId, SessionState.Paused, "Break");

        var result = await handler.Handle(command, CancellationToken.None);

        result.CurrentState.Should().Be(nameof(SessionState.Paused));
        result.Timer.Should().NotBeNull();
        result.Timer!.RemainingSeconds.Should().Be(2160);
        result.Timer.TimerStatus.Should().Be("Frozen");
        result.Timer.IsAdvancing.Should().BeFalse();
        result.Timer.AdvancingSince.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenResumingPausedSession_ReturnsAdvancingTimerSnapshotFromFrozenRemainder()
    {
        var session = CreateScheduledSession(assignedOperatorUserId: 42, registerTeam: true);
        var transitionPolicy = new SessionStateTransitionPolicy();
        var activeAt = Now.AddMinutes(-12);
        var pausedAt = Now.AddMinutes(-7);
        session.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), transitionPolicy);
        session.MoveTo(SessionState.Active, activeAt, transitionPolicy);
        session.MoveTo(SessionState.Paused, pausedAt, transitionPolicy);
        var repository = CreateRepository(session);
        var handler = CreateHandler(repository, CreateCurrentUser("kc-operator-42", "Operator"), resolvedUserId: 42);
        var command = new TransitionSessionStateCommand(session.LiveSessionId, SessionState.Active, "Continue");

        var result = await handler.Handle(command, CancellationToken.None);

        result.CurrentState.Should().Be(nameof(SessionState.Active));
        result.Timer.Should().NotBeNull();
        result.Timer!.RemainingSeconds.Should().Be(2400);
        result.Timer.TimerStatus.Should().Be("Advancing");
        result.Timer.IsAdvancing.Should().BeTrue();
        result.Timer.AdvancingSince.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_WithStructurallyInvalidTransition_ThrowsAndDoesNotPersist()
    {
        var session = CreateScheduledSession(assignedOperatorUserId: 42, registerTeam: true);
        var repository = CreateRepository(session);
        var handler = CreateHandler(repository, CreateCurrentUser("kc-operator-42", "Operator"), resolvedUserId: 42);
        var command = new TransitionSessionStateCommand(session.LiveSessionId, SessionState.Finished, null);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidSessionStateTransitionException>();
        session.State.Should().Be(SessionState.Scheduled);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenActivatingWithoutTeams_ThrowsRequiresAtLeastOneTeam()
    {
        var session = CreateScheduledSession(assignedOperatorUserId: 42, registerTeam: false);
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-1), new SessionStateTransitionPolicy());
        var repository = CreateRepository(session);
        // Administrator caller passes the access proxy so the chain's liveness gate is reached.
        var handler = CreateHandler(repository, CreateCurrentUser("99", "Administrator"));
        var command = new TransitionSessionStateCommand(session.LiveSessionId, SessionState.Active, null);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<LiveSessionRequiresAtLeastOneTeamException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOperatorNotAssigned_ThrowsSessionOperatorNotAssigned()
    {
        var session = CreateScheduledSession(assignedOperatorUserId: null, registerTeam: true);
        var repository = CreateRepository(session);
        var handler = CreateHandler(repository, CreateCurrentUser("99", "Administrator"));
        var command = new TransitionSessionStateCommand(session.LiveSessionId, SessionState.Preparing, null);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<SessionOperatorNotAssignedException>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotAssignedOperator_ThrowsForbidden()
    {
        var session = CreateScheduledSession(assignedOperatorUserId: 42, registerTeam: true);
        var repository = CreateRepository(session);
        var handler = CreateHandler(repository, CreateCurrentUser("kc-operator-77", "Operator"), resolvedUserId: 77);
        var command = new TransitionSessionStateCommand(session.LiveSessionId, SessionState.Preparing, null);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    private static TransitionSessionStateCommandHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        Mock<ICurrentUser> currentUser,
        int resolvedUserId = 99)
    {
        var accessExecutor = new SessionAdministrationAccessResolver(repository.Object);
        var actorClient = new Mock<IAuthenticatedActorProfileAccessClient>();
        actorClient
            .Setup(client => client.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedActorProfileLookupDto(
                resolvedUserId,
                currentUser.Object.Id ?? "missing",
                currentUser.Object.Role ?? "Unknown",
                true));
        var accessResolver = new SessionAdministrationAuthorizationProxy(currentUser.Object, accessExecutor, actorClient.Object);
        var transitionPolicy = new SessionStateTransitionPolicy();
        var chain = new SessionTransitionChain(new SessionTransitionValidator[]
        {
            new CurrentStateGate(transitionPolicy),
            new OperatorAssignmentGate(),
            new ParticipantReadinessGate()
        });
        var facade = new TransitionSessionStateFacade(
            accessResolver,
            chain,
            transitionPolicy,
            repository.Object,
            new FixedTimeProvider(Now));

        return new TransitionSessionStateCommandHandler(facade);
    }

    private static Mock<ILiveSessionRepository> CreateRepository(LiveSession session)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repository
            .Setup(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return repository;
    }

    private static Mock<ICurrentUser> CreateCurrentUser(string? id, string? role)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(id);
        currentUser.SetupGet(user => user.Role).Returns(role);
        return currentUser;
    }

    private static LiveSession CreateScheduledSession(int? assignedOperatorUserId, bool registerTeam)
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt(
            $"SES-{Guid.NewGuid():N}"[..12],
            "Lifecycle Session",
            45,
            Now.AddHours(1));

        if (registerTeam)
        {
            session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
        }

        if (assignedOperatorUserId is not null)
        {
            session.AssignOperator(assignedOperatorUserId.Value, Now.AddMinutes(-5));
        }

        return session;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
