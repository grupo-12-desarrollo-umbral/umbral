using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.DisconnectParticipant;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.DisconnectParticipant;

public sealed class DisconnectParticipantCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenParticipantIsActive_MarksParticipantDisconnected()
    {
        var session = CreateActiveSessionWithParticipant(out var participantId);
        var repository = CreateRepository(session);
        var currentUser = CreateCurrentUser(Guid.NewGuid());
        var disconnectedAt = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var command = new DisconnectParticipantCommand(session.LiveSessionId, participantId);
        var handler = CreateHandler(repository, currentUser, new FixedTimeProvider(disconnectedAt));

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().Be(MediatR.Unit.Value);
        session.Participants.Single(participant => participant.SessionParticipantId == participantId).IsDisconnected.Should().BeTrue();
        session.Participants.Single(participant => participant.SessionParticipantId == participantId).LastSeenAt.Should().Be(disconnectedAt);
        repository.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSessionDoesNotExist_ThrowsNotFoundException()
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveSession?)null);
        var currentUser = CreateCurrentUser(Guid.NewGuid());
        var command = new DisconnectParticipantCommand(Guid.NewGuid(), Guid.NewGuid());
        var handler = CreateHandler(repository, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DisconnectParticipantCommandHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        Mock<ICurrentUser> currentUser,
        TimeProvider timeProvider)
    {
        var executor = new DisconnectParticipantService(
            repository.Object,
            timeProvider);
        var proxy = new DisconnectParticipantAuthorizationProxy(currentUser.Object, executor);

        return new DisconnectParticipantCommandHandler(proxy);
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

    private static Mock<ICurrentUser> CreateCurrentUser(Guid participantIdentity)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(participantIdentity.ToString());
        currentUser.SetupGet(user => user.Role).Returns("Participant");
        return currentUser;
    }

    private static LiveSession CreateActiveSessionWithParticipant(out Guid participantId)
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt(
            "disc123",
            "Disconnect Session",
            45,
            new DateTimeOffset(2026, 6, 4, 11, 0, 0, TimeSpan.Zero));

        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        participantId = session.AdmitParticipant(
            Guid.NewGuid(),
            "Nora",
            team.TeamId,
            new DateTimeOffset(2026, 6, 4, 11, 3, 0, TimeSpan.Zero),
            new JoinPolicy()).Participant.SessionParticipantId;
        session.MoveTo(SessionState.Preparing, new DateTimeOffset(2026, 6, 4, 11, 1, 0, TimeSpan.Zero), new SessionStateTransitionPolicy());
        session.MoveTo(SessionState.Active, new DateTimeOffset(2026, 6, 4, 11, 2, 0, TimeSpan.Zero), new SessionStateTransitionPolicy());

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
