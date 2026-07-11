using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Queries.GetSessionTeamLobby;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetSessionTeamLobby;

// Participant team lobby read (#108): each attached team is tagged mine/joinable/locked from the
// participant's eligible-teams whitelist intersected with the #88 Open Team Selection policy.
public sealed class GetSessionTeamLobbyByCodeQueryHandlerTests
{
    private static readonly Guid RedRef = Guid.NewGuid();
    private static readonly Guid BlueRef = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 6, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenEligibleWithSubset_MarksWhitelistedJoinableAndOthersLocked()
    {
        var session = CreateSession(out var red, out var blue);
        var handler = CreateHandler(session, Guid.NewGuid(), Eligible(RedRef));

        var result = await Handle(handler, session.SessionCode);

        JoinState(result, red.TeamId).Should().Be("joinable");
        JoinState(result, blue.TeamId).Should().Be("locked");
    }

    [Fact]
    public async Task Handle_WhenEligibleWithEmptyWhitelist_MarksAllAttachedJoinable()
    {
        var session = CreateSession(out var red, out var blue);
        var handler = CreateHandler(session, Guid.NewGuid(), Eligible());

        var result = await Handle(handler, session.SessionCode);

        JoinState(result, red.TeamId).Should().Be("joinable");
        JoinState(result, blue.TeamId).Should().Be("joinable");
    }

    [Fact]
    public async Task Handle_WhenNotEligible_MarksEveryTeamLocked()
    {
        var session = CreateSession(out var red, out var blue);
        var handler = CreateHandler(session, Guid.NewGuid(), NotEligible());

        var result = await Handle(handler, session.SessionCode);

        result.Teams.Should().OnlyContain(team => team.JoinState == "locked");
        JoinState(result, red.TeamId).Should().Be("locked");
        JoinState(result, blue.TeamId).Should().Be("locked");
    }

    [Fact]
    public async Task Handle_WhenParticipantHoldsATeam_MarksItMine()
    {
        var session = CreateSession(out var red, out var blue);
        var identity = Guid.NewGuid();
        session.SelectTeam(identity, "Nora", red.TeamId, new HashSet<Guid>(), At, new OpenTeamSelectionPolicy());
        var handler = CreateHandler(session, identity, Eligible());

        var result = await Handle(handler, session.SessionCode);

        JoinState(result, red.TeamId).Should().Be("mine");
        JoinState(result, blue.TeamId).Should().Be("joinable");
    }

    [Fact]
    public async Task Handle_WhenSelectableTeamIsFull_MarksItLocked()
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt();
        var red = session.AssociateTeam(RedRef, "Red", "RED-01", capacity: 1);
        session.SelectTeam(Guid.NewGuid(), "Filler", red.TeamId, new HashSet<Guid>(), At, new OpenTeamSelectionPolicy());
        var handler = CreateHandler(session, Guid.NewGuid(), Eligible(RedRef));

        var result = await Handle(handler, session.SessionCode);

        JoinState(result, red.TeamId).Should().Be("locked");
    }

    [Fact]
    public async Task Handle_WhenSessionFrozen_MarksNoTeamJoinable()
    {
        var session = CreateSession(out var red, out var blue);
        var identity = Guid.NewGuid();
        session.SelectTeam(identity, "Nora", red.TeamId, new HashSet<Guid>(), At, new OpenTeamSelectionPolicy());
        var transitions = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, At, transitions);
        session.MoveTo(SessionState.Active, At, transitions);
        var handler = CreateHandler(session, identity, Eligible());

        var result = await Handle(handler, session.SessionCode);

        JoinState(result, red.TeamId).Should().Be("mine"); // current membership survives the freeze
        JoinState(result, blue.TeamId).Should().Be("locked");
        result.Teams.Should().NotContain(team => team.JoinState == "joinable");
    }

    [Fact]
    public async Task Handle_ProjectsReferenceTeamIdPerTeam()
    {
        // The membership guard on validate/reconnect/answer-submit keys off the reference id, so the
        // lobby must surface it alongside the runtime TeamId (which the self-join route targets).
        var session = CreateSession(out var red, out var blue);
        var handler = CreateHandler(session, Guid.NewGuid(), Eligible());

        var result = await Handle(handler, session.SessionCode);

        result.Teams.Single(team => team.TeamId == red.TeamId).ReferenceTeamId.Should().Be(RedRef);
        result.Teams.Single(team => team.TeamId == blue.TeamId).ReferenceTeamId.Should().Be(BlueRef);
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_ThrowsNotFound()
    {
        var handler = CreateHandler(
            CreateRepository(null, "MISSING"),
            Guid.NewGuid(),
            Eligible());

        var act = async () => await Handle(handler, "MISSING");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIdIsNotAGuid_ThrowsUnauthorizedException()
    {
        var session = CreateSession(out _, out _);
        var repository = CreateRepository(session, session.SessionCode);
        var eligibleTeams = new Mock<IParticipantEligibleTeamsClient>();
        eligibleTeams
            .Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Eligible());
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns("not-a-guid");
        var handler = new GetSessionTeamLobbyByCodeQueryHandler(
            repository.Object,
            eligibleTeams.Object,
            currentUser.Object,
            new OpenTeamSelectionPolicy());

        var act = async () => await Handle(handler, session.SessionCode);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        eligibleTeams.Verify(client => client.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Task<SessionTeamLobbyDto> Handle(GetSessionTeamLobbyByCodeQueryHandler handler, string sessionCode)
    {
        return handler.Handle(new GetSessionTeamLobbyByCodeQuery(sessionCode), CancellationToken.None);
    }

    private static string JoinState(SessionTeamLobbyDto result, Guid teamId)
    {
        return result.Teams.Single(team => team.TeamId == teamId).JoinState;
    }

    private static LiveSession CreateSession(out Team red, out Team blue)
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt();
        red = session.AssociateTeam(RedRef, "Red", "RED-01", capacity: 4);
        blue = session.AssociateTeam(BlueRef, "Blue", "BLU-01", capacity: 4);
        return session;
    }

    private static GetSessionTeamLobbyByCodeQueryHandler CreateHandler(
        LiveSession session,
        Guid participantIdentity,
        ParticipantEligibleTeamsDto whitelist)
    {
        return CreateHandler(CreateRepository(session, session.SessionCode), participantIdentity, whitelist);
    }

    private static GetSessionTeamLobbyByCodeQueryHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        Guid participantIdentity,
        ParticipantEligibleTeamsDto whitelist)
    {
        var eligibleTeams = new Mock<IParticipantEligibleTeamsClient>();
        eligibleTeams
            .Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(whitelist);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(participantIdentity.ToString());

        return new GetSessionTeamLobbyByCodeQueryHandler(
            repository.Object,
            eligibleTeams.Object,
            currentUser.Object,
            new OpenTeamSelectionPolicy());
    }

    private static Mock<ILiveSessionRepository> CreateRepository(LiveSession? session, string sessionCode)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetBySessionCodeAsync(sessionCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        return repository;
    }

    private static ParticipantEligibleTeamsDto Eligible(params Guid[] referenceTeamIds)
    {
        var teams = referenceTeamIds
            .Select(id => new EligibleTeamDto(id, "Team", "T-01"))
            .ToList();

        return new ParticipantEligibleTeamsDto(true, "eligible", teams);
    }

    private static ParticipantEligibleTeamsDto NotEligible()
    {
        return new ParticipantEligibleTeamsDto(false, "deactivated", Array.Empty<EligibleTeamDto>());
    }
}
