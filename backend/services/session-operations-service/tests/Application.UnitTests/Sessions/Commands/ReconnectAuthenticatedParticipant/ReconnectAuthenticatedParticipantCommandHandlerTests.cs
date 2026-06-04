using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Handlers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.ReconnectAuthenticatedParticipant;

public sealed class ReconnectAuthenticatedParticipantCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenDisconnectedParticipantReturnsToAssignedTeam_ReconnectsAndReturnsRuntimeContext()
    {
        var session = CreateScheduledSession();
        var identityReferenceTeamId = Guid.NewGuid();
        var team = session.AssociateTeam(identityReferenceTeamId, "Alpha", "A-01", 4);
        var participantIdentity = Guid.NewGuid();
        var firstAdmission = session.AdmitParticipant(
            participantIdentity,
            "Nora",
            team.TeamId,
            new DateTimeOffset(2026, 6, 3, 10, 5, 0, TimeSpan.Zero),
            new JoinPolicy());
        session.DisconnectParticipant(
            firstAdmission.Participant.SessionParticipantId,
            new DateTimeOffset(2026, 6, 3, 10, 6, 0, TimeSpan.Zero));

        var repository = CreateRepository(session);
        var accessClient = CreateAccessClient(session.LiveSessionId, team.TeamId, isAllowed: true);
        var currentUser = CreateCurrentUser(participantIdentity);
        var command = new ReconnectAuthenticatedParticipantCommand(session.LiveSessionId, team.TeamId, "Nora", "join-token");
        var handler = CreateHandler(repository, accessClient, currentUser, new FixedTimeProvider(new DateTimeOffset(2026, 6, 3, 10, 7, 0, TimeSpan.Zero)));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsReconnect.Should().BeTrue();
        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.TeamId.Should().Be(team.TeamId);
        result.TeamDisplayName.Should().Be("Alpha");
        result.ParticipantDisplayName.Should().Be("Nora");
        result.SessionState.Should().Be(SessionState.Scheduled.ToString());
        result.LastSeenAt.Should().Be(new DateTimeOffset(2026, 6, 3, 10, 7, 0, TimeSpan.Zero));
        repository.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenLateJoinTargetsActiveSession_ThrowsException()
    {
        var session = CreateActiveSession();
        var team = session.Teams.Single();
        var repository = CreateRepository(session);
        var accessClient = CreateAccessClient(session.LiveSessionId, team.TeamId, isAllowed: true);
        var currentUser = CreateCurrentUser(Guid.NewGuid());
        var command = new ReconnectAuthenticatedParticipantCommand(session.LiveSessionId, team.TeamId, "Nova", null);
        var handler = CreateHandler(repository, accessClient, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<LateJoinNotAllowedException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenIdentityDeniesMembershipAccess_ThrowsForbiddenException()
    {
        var session = CreateScheduledSession();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var repository = CreateRepository(session);
        var accessClient = CreateAccessClient(session.LiveSessionId, team.TeamId, isAllowed: false);
        var currentUser = CreateCurrentUser(Guid.NewGuid());
        var command = new ReconnectAuthenticatedParticipantCommand(session.LiveSessionId, team.TeamId, "Nora", null);
        var handler = CreateHandler(repository, accessClient, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        repository.Verify(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDisconnectedParticipantTargetsFinishedSession_ThrowsException()
    {
        var transitionPolicy = new SessionStateTransitionPolicy();
        var session = CreateScheduledSession();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var participantIdentity = Guid.NewGuid();
        var admission = session.AdmitParticipant(
            participantIdentity,
            "Nora",
            team.TeamId,
            new DateTimeOffset(2026, 6, 3, 10, 5, 0, TimeSpan.Zero),
            new JoinPolicy());

        session.DisconnectParticipant(
            admission.Participant.SessionParticipantId,
            new DateTimeOffset(2026, 6, 3, 10, 6, 0, TimeSpan.Zero));
        session.MoveTo(SessionState.Preparing, new DateTimeOffset(2026, 6, 3, 10, 7, 0, TimeSpan.Zero), transitionPolicy);
        session.MoveTo(SessionState.Active, new DateTimeOffset(2026, 6, 3, 10, 8, 0, TimeSpan.Zero), transitionPolicy);
        session.MoveTo(SessionState.Finished, new DateTimeOffset(2026, 6, 3, 10, 9, 0, TimeSpan.Zero), transitionPolicy);

        var repository = CreateRepository(session);
        var accessClient = CreateAccessClient(session.LiveSessionId, team.TeamId, isAllowed: true);
        var currentUser = CreateCurrentUser(participantIdentity);
        var command = new ReconnectAuthenticatedParticipantCommand(session.LiveSessionId, team.TeamId, "Nora", null);
        var handler = CreateHandler(repository, accessClient, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<LateJoinNotAllowedException>();
    }

    [Fact]
    public async Task Handle_WhenTeamIsAtCapacity_ThrowsException()
    {
        var session = CreateScheduledSession();
        var team = session.RegisterTeam("Alpha", "A-01", 2);
        session.AdmitParticipant(Guid.NewGuid(), "P1", team.TeamId, DateTimeOffset.UtcNow.AddMinutes(-2), new JoinPolicy());
        session.AdmitParticipant(Guid.NewGuid(), "P2", team.TeamId, DateTimeOffset.UtcNow.AddMinutes(-1), new JoinPolicy());

        var repository = CreateRepository(session);
        var accessClient = CreateAccessClient(session.LiveSessionId, team.TeamId, isAllowed: true);
        var currentUser = CreateCurrentUser(Guid.NewGuid());
        var command = new ReconnectAuthenticatedParticipantCommand(session.LiveSessionId, team.TeamId, "P3", null);
        var handler = CreateHandler(repository, accessClient, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<TeamCapacityReachedException>();
    }

    private static ReconnectAuthenticatedParticipantCommandHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        Mock<IParticipantMembershipAccessClient> accessClient,
        Mock<ICurrentUser> currentUser,
        TimeProvider timeProvider)
    {
        var executor = new ReconnectAuthenticatedParticipantService(
            repository.Object,
            accessClient.Object,
            currentUser.Object,
            new JoinPolicy(),
            timeProvider);
        var proxy = new ReconnectAuthenticatedParticipantAuthorizationProxy(currentUser.Object, executor);

        return new ReconnectAuthenticatedParticipantCommandHandler(proxy);
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

    private static Mock<IParticipantMembershipAccessClient> CreateAccessClient(
        Guid liveSessionId,
        Guid teamId,
        bool isAllowed)
    {
        var accessClient = new Mock<IParticipantMembershipAccessClient>();
        accessClient
            .Setup(client => client.ValidateAsync(liveSessionId, teamId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParticipantMembershipAccessDecisionDto(
                "ParticipantExperience",
                isAllowed,
                isAllowed ? "allowed" : "denied",
                liveSessionId,
                teamId));

        return accessClient;
    }

    private static Mock<ICurrentUser> CreateCurrentUser(Guid participantIdentity)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(participantIdentity.ToString());
        currentUser.SetupGet(user => user.Role).Returns("Participant");
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
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero));
    }

    private static LiveSession CreateActiveSession()
    {
        var session = CreateScheduledSession();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero), transitionPolicy);
        session.MoveTo(SessionState.Active, new DateTimeOffset(2026, 6, 3, 10, 2, 0, TimeSpan.Zero), transitionPolicy);
        return session;
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
