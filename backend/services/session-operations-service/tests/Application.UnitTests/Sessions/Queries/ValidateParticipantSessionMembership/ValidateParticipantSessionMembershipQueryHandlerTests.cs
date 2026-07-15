using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Queries.ValidateParticipantSessionMembership;
using umbral_backend.Application.UnitTests.TestData;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.ValidateParticipantSessionMembership;

public sealed class ValidateParticipantSessionMembershipQueryHandlerTests
{
    private static readonly Guid ExternalIdentityId = Guid.NewGuid();
    private static readonly DateTimeOffset JoinedAt = new(2026, 6, 3, 10, 5, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenParticipantIsActiveAndAssignedToTeam_ReturnsAllowed()
    {
        var (session, teamId) = CreateSessionWithParticipant();
        var handler = CreateHandler(session, ExternalIdentityId);

        var result = await handler.Handle(
            new ValidateParticipantSessionMembershipQuery(session.LiveSessionId, teamId),
            CancellationToken.None);

        result.IsAllowed.Should().BeTrue();
        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.TeamId.Should().Be(teamId);
        result.ReasonCode.Should().Be("allowed");
    }

    [Fact]
    public async Task Handle_WhenUserIdIsNotGuid_ReturnsDenyUnauthenticated()
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt();
        var handler = CreateHandler(session, currentUserId: "not-a-guid");

        var result = await handler.Handle(
            new ValidateParticipantSessionMembershipQuery(session.LiveSessionId, Guid.NewGuid()),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be("unauthenticated");
    }

    [Fact]
    public async Task Handle_WhenSessionDoesNotExist_ReturnsDenySessionNotFound()
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveSession?)null);
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.Id).Returns(ExternalIdentityId.ToString());
        var handler = new ValidateParticipantSessionMembershipQueryHandler(
            repository.Object,
            currentUser.Object,
            new ParticipantSessionMembershipChecker(currentUser.Object));

        var result = await handler.Handle(
            new ValidateParticipantSessionMembershipQuery(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be("session-not-found");
    }

    [Fact]
    public async Task Handle_WhenParticipantNotInSession_ReturnsDeny()
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var handler = CreateHandler(session, Guid.NewGuid());

        var result = await handler.Handle(
            new ValidateParticipantSessionMembershipQuery(session.LiveSessionId, team.ReferenceTeamId!.Value),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be("participant-not-in-session");
    }

    [Fact]
    public async Task Handle_WhenParticipantIsBlocked_ReturnsDeny()
    {
        var (session, teamId) = CreateSessionWithParticipant();
        session.Participants.Single().Block(JoinedAt.AddMinutes(1));
        var handler = CreateHandler(session, ExternalIdentityId);

        var result = await handler.Handle(
            new ValidateParticipantSessionMembershipQuery(session.LiveSessionId, teamId),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be("participant-blocked-or-removed");
    }

    [Fact]
    public async Task Handle_WhenParticipantIsRemoved_ReturnsDeny()
    {
        var (session, teamId) = CreateSessionWithParticipant();
        session.Participants.Single().Remove(JoinedAt.AddMinutes(1));
        var handler = CreateHandler(session, ExternalIdentityId);

        var result = await handler.Handle(
            new ValidateParticipantSessionMembershipQuery(session.LiveSessionId, teamId),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be("participant-blocked-or-removed");
    }

    [Fact]
    public async Task Handle_WhenTeamNotInSession_ReturnsDeny()
    {
        var (session, _) = CreateSessionWithParticipant();
        var handler = CreateHandler(session, ExternalIdentityId);

        var result = await handler.Handle(
            new ValidateParticipantSessionMembershipQuery(session.LiveSessionId, Guid.NewGuid()),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be("team-not-in-session");
    }

    [Fact]
    public async Task Handle_WhenParticipantAssignedToDifferentTeam_ReturnsDeny()
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt();
        var teamAlpha = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var teamBeta = session.AssociateTeam(Guid.NewGuid(), "Beta", "B-01", 4);
        session.AdmitParticipant(ExternalIdentityId, "Alice", teamAlpha.TeamId, JoinedAt, new JoinPolicy());
        var handler = CreateHandler(session, ExternalIdentityId);

        var result = await handler.Handle(
            new ValidateParticipantSessionMembershipQuery(session.LiveSessionId, teamBeta.ReferenceTeamId!.Value),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be("participant-not-assigned-to-team");
    }

    private static ValidateParticipantSessionMembershipQueryHandler CreateHandler(
        LiveSession session, Guid currentUserId)
    {
        return CreateHandler(session, currentUserId.ToString());
    }

    private static ValidateParticipantSessionMembershipQueryHandler CreateHandler(
        LiveSession session, string currentUserId)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(r => r.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.Id).Returns(currentUserId);

        return new ValidateParticipantSessionMembershipQueryHandler(
            repository.Object,
            currentUser.Object,
            new ParticipantSessionMembershipChecker(currentUser.Object));
    }

    private static (LiveSession Session, Guid TeamId) CreateSessionWithParticipant()
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        session.AdmitParticipant(ExternalIdentityId, "Alice", team.TeamId, JoinedAt, new JoinPolicy());
        return (session, team.ReferenceTeamId!.Value);
    }
}
