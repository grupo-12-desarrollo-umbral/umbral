using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.AddOperativeClue;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.AddOperativeClue;

public sealed class AddOperativeClueCommandHandlerTests
{
    private const int AssignedOperatorUserId = 42;
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenOneTeamIsAssigned_AddsAndReturnsOneOperativeClue()
    {
        var session = CreateActiveSession(out var alpha, out _);
        var repository = CreateRepository(session);
        var actorClient = CreateActorClient(AssignedOperatorUserId);
        var handler = CreateHandler(repository, actorClient, AssignedOperatorUserId);
        var command = new AddOperativeClueCommand(session.LiveSessionId, "  Check the clock.  ", [alpha.TeamId]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.OperativeClueIds.Should().ContainSingle().Which.Should().NotBeEmpty();
        result.AssignedTeamIds.Should().Equal(alpha.TeamId);
        result.ClueText.Should().Be("Check the clock.");
        session.GetOperativeClues().Should().ContainSingle(clue =>
            clue.OperativeClueId == result.OperativeClueIds[0] &&
            clue.TeamId == alpha.TeamId &&
            clue.CreatedByUserId == AssignedOperatorUserId &&
            clue.CreatedAt == Now);
        repository.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSeveralTeamsAreAssigned_FansOutWithoutAdvancingSubstage()
    {
        var session = CreateActiveSession(out var alpha, out var bravo);
        var activeSubstageId = session.ActiveSubstageId;
        var repository = CreateRepository(session);
        var actorClient = CreateActorClient(AssignedOperatorUserId);
        var handler = CreateHandler(repository, actorClient, AssignedOperatorUserId);
        var command = new AddOperativeClueCommand(
            session.LiveSessionId,
            "Check the clock.",
            [alpha.TeamId, bravo.TeamId]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.OperativeClueIds.Should().HaveCount(2).And.OnlyHaveUniqueItems();
        result.AssignedTeamIds.Should().Equal(alpha.TeamId, bravo.TeamId);
        session.GetOperativeClues().Should().HaveCount(2);
        session.DomainEvents.OfType<OperativeClueAddedEvent>().Should().HaveCount(2);
        session.ActiveSubstageId.Should().Be(activeSubstageId);
        session.ProjectParticipantTeamBoard(alpha.TeamId, Now)
            .ActiveSubstageContext!.ResolvedTargets.Should().Be(0);
        session.DomainEvents.OfType<SubstageAdvancedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenOperatorDoesNotOwnSession_ThrowsForbiddenAccessException()
    {
        var session = CreateActiveSession(out var alpha, out _);
        var repository = CreateRepository(session);
        var actorClient = CreateActorClient(84);
        var handler = CreateHandler(repository, actorClient, 84);
        var command = new AddOperativeClueCommand(session.LiveSessionId, "Check the clock.", [alpha.TeamId]);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        session.GetOperativeClues().Should().BeEmpty();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionIsNotLive_PropagatesDomainRejectionWithoutPersisting()
    {
        var session = CreateScheduledSession(out var alpha);
        var repository = CreateRepository(session);
        var actorClient = CreateActorClient(AssignedOperatorUserId);
        var handler = CreateHandler(repository, actorClient, AssignedOperatorUserId);
        var command = new AddOperativeClueCommand(session.LiveSessionId, "Check the clock.", [alpha.TeamId]);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<SessionNotLiveForOperativeClueException>();
        session.GetOperativeClues().Should().BeEmpty();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AddOperativeClueCommandHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        Mock<IAuthenticatedActorProfileAccessClient> actorClient,
        int currentActorUserId)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns($"external-{currentActorUserId}");
        currentUser.SetupGet(user => user.Role).Returns("Operator");
        var resolver = new SessionAdministrationAuthorizationProxy(
            currentUser.Object,
            repository.Object,
            actorClient.Object);

        return new AddOperativeClueCommandHandler(
            resolver,
            actorClient.Object,
            repository.Object,
            new FixedTimeProvider(Now));
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

    private static Mock<IAuthenticatedActorProfileAccessClient> CreateActorClient(int userId)
    {
        var actorClient = new Mock<IAuthenticatedActorProfileAccessClient>();
        actorClient
            .Setup(client => client.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedActorProfileLookupDto(
                userId,
                $"external-{userId}",
                "Operator",
                true));
        return actorClient;
    }

    private static LiveSession CreateActiveSession(out Team alpha, out Team bravo)
    {
        var session = CreateScheduledSession(out alpha);
        bravo = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, Now.AddMinutes(-1), policy);
        return session;
    }

    private static LiveSession CreateScheduledSession(out Team alpha)
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt();
        session.AssignOperator(AssignedOperatorUserId, Now.AddMinutes(-4));
        alpha = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        session.ClearDomainEvents();
        return session;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
