using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Handlers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.AssignOperatorToSession;

public sealed class AssignOperatorToSessionCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenSessionHasNoAssignedOperator_AssignsEligibleOperator()
    {
        var session = CreateScheduledSession();
        var repository = CreateRepository(session);
        var eligibilityClient = CreateEligibilityClient(isEligible: true, role: "Operator");
        var currentUser = CreateCurrentUser("99", "Administrator");
        var command = new AssignOperatorToSessionCommand(session.LiveSessionId, 27);
        var handler = CreateHandler(
            repository,
            eligibilityClient,
            currentUser,
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero)));

        var result = await handler.Handle(command, CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.AssignedOperatorUserId.Should().Be(27);
        session.AssignedOperatorUserId.Should().Be(27);
        session.DomainEvents.OfType<LiveSessionOperatorAssignedEvent>()
            .Last()
            .PreviousOperatorUserId.Should().BeNull();
        repository.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSessionAlreadyHasAssignedOperator_ReassignsEligibleOperator()
    {
        var session = CreateScheduledSession();
        session.AssignOperator(27, new DateTimeOffset(2026, 6, 4, 11, 0, 0, TimeSpan.Zero));
        var repository = CreateRepository(session);
        var eligibilityClient = CreateEligibilityClient(isEligible: true, role: "Administrator");
        var currentUser = CreateCurrentUser("99", "Administrator");
        var command = new AssignOperatorToSessionCommand(session.LiveSessionId, 31);
        var handler = CreateHandler(
            repository,
            eligibilityClient,
            currentUser,
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 4, 12, 30, 0, TimeSpan.Zero)));

        var result = await handler.Handle(command, CancellationToken.None);

        result.AssignedOperatorUserId.Should().Be(31);
        session.AssignedOperatorUserId.Should().Be(31);

        var assignmentEvent = session.DomainEvents
            .OfType<LiveSessionOperatorAssignedEvent>()
            .Last();

        assignmentEvent.PreviousOperatorUserId.Should().Be(27);
        assignmentEvent.AssignedOperatorUserId.Should().Be(31);
    }

    [Fact]
    public async Task Handle_WhenSessionIsUnknown_ThrowsNotFoundException()
    {
        var repository = new Mock<ILiveSessionRepository>();
        var eligibilityClient = CreateEligibilityClient(isEligible: true, role: "Operator");
        var currentUser = CreateCurrentUser("99", "Administrator");
        var command = new AssignOperatorToSessionCommand(Guid.NewGuid(), 27);
        var handler = CreateHandler(
            repository,
            eligibilityClient,
            currentUser,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        eligibilityClient.Verify(client => client.GetEligibilityAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTargetActorIsIneligible_ThrowsIneligibleSessionOperatorException()
    {
        var session = CreateScheduledSession();
        var repository = CreateRepository(session);
        var eligibilityClient = CreateEligibilityClient(isEligible: false, role: "Participant");
        var currentUser = CreateCurrentUser("99", "Administrator");
        var command = new AssignOperatorToSessionCommand(session.LiveSessionId, 27);
        var handler = CreateHandler(
            repository,
            eligibilityClient,
            currentUser,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<IneligibleSessionOperatorException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCallerIsUnauthenticated_ThrowsUnauthorizedException()
    {
        var session = CreateScheduledSession();
        var repository = CreateRepository(session);
        var eligibilityClient = CreateEligibilityClient(isEligible: true, role: "Operator");
        var currentUser = CreateCurrentUser(null, null);
        var command = new AssignOperatorToSessionCommand(session.LiveSessionId, 27);
        var handler = CreateHandler(
            repository,
            eligibilityClient,
            currentUser,
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        eligibilityClient.Verify(client => client.GetEligibilityAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AssignOperatorToSessionCommandHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        Mock<IAssignableSessionOperatorAccessClient> eligibilityClient,
        Mock<ICurrentUser> currentUser,
        TimeProvider timeProvider)
    {
        var accessExecutor = new SessionAdministrationAccessResolver(repository.Object);
        var accessResolver = new SessionAdministrationAuthorizationProxy(currentUser.Object, accessExecutor);
        var facade = new AssignOperatorToSessionFacade(
            accessResolver,
            eligibilityClient.Object,
            repository.Object,
            timeProvider);

        return new AssignOperatorToSessionCommandHandler(facade);
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

    private static Mock<IAssignableSessionOperatorAccessClient> CreateEligibilityClient(bool isEligible, string role)
    {
        var client = new Mock<IAssignableSessionOperatorAccessClient>();
        client
            .Setup(access => access.GetEligibilityAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int operatorUserId, CancellationToken _) => new SessionOperatorEligibilityDecisionDto(
                "Identity",
                isEligible,
                operatorUserId,
                role,
                isEligible ? null : "Target actor is not assignable."));

        return client;
    }

    private static Mock<ICurrentUser> CreateCurrentUser(string? id, string? role)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(id);
        currentUser.SetupGet(user => user.Role).Returns(role);
        return currentUser;
    }

    private static LiveSession CreateScheduledSession()
    {
        return LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            "abc123",
            "Museum Hunt",
            45,
            new DateTimeOffset(2026, 6, 4, 10, 0, 0, TimeSpan.Zero));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
