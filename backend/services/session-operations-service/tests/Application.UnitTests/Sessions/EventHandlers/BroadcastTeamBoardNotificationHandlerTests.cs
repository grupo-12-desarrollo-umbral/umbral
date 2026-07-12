using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Application.UnitTests.TestData;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

public sealed class BroadcastTeamBoardNotificationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_SessionStateChanged_BroadcastsTreasureHuntBoardOnceForTheTeam()
    {
        var session = CreateActiveTreasureHunt(out var teamIds);
        var broadcaster = new Mock<ITeamBoardBroadcaster>();
        var handler = CreateHandler(session, broadcaster);

        await handler.Handle(
            new SessionStateChangedEvent(session.LiveSessionId, SessionState.Preparing, SessionState.Active, Now),
            CancellationToken.None);

        broadcaster.Verify(
            current => current.BroadcastTeamBoardUpdatedAsync(
                It.Is<ParticipantTeamBoardDto>(board =>
                    board.LiveSessionId == session.LiveSessionId &&
                    board.TeamId == teamIds[0] &&
                    board.ActiveSubstage != null &&
                    board.ActiveSubstage.PlayMode == SubstagePlayMode.TreasureHunt.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
        broadcaster.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_SubstageAdvanced_BroadcastsTreasureHuntBoardOnceForTheTeam()
    {
        var session = CreateActiveTreasureHunt(out var teamIds);
        var broadcaster = new Mock<ITeamBoardBroadcaster>();
        var handler = CreateHandler(session, broadcaster);

        await handler.Handle(
            new SubstageAdvancedEvent(
                session.LiveSessionId,
                session.ActiveSubstageId!.Value,
                SubstagePlayMode.TreasureHunt,
                toSubstageId: null,
                Now),
            CancellationToken.None);

        broadcaster.Verify(
            current => current.BroadcastTeamBoardUpdatedAsync(
                It.Is<ParticipantTeamBoardDto>(board =>
                    board.TeamId == teamIds[0] &&
                    board.ActiveSubstage != null &&
                    board.ActiveSubstage.PlayMode == SubstagePlayMode.TreasureHunt.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
        broadcaster.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_MultiTeamSession_BroadcastsOncePerTeam()
    {
        var session = CreateActiveTreasureHunt(out var teamIds, teamCount: 3);
        var broadcaster = new Mock<ITeamBoardBroadcaster>();
        var handler = CreateHandler(session, broadcaster);

        await handler.Handle(
            new SessionStateChangedEvent(session.LiveSessionId, SessionState.Preparing, SessionState.Active, Now),
            CancellationToken.None);

        broadcaster.Verify(
            current => current.BroadcastTeamBoardUpdatedAsync(
                It.IsAny<ParticipantTeamBoardDto>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(teamIds.Count));
        foreach (var teamId in teamIds)
        {
            broadcaster.Verify(
                current => current.BroadcastTeamBoardUpdatedAsync(
                    It.Is<ParticipantTeamBoardDto>(board => board.TeamId == teamId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    private static BroadcastTeamBoardNotificationHandler CreateHandler(
        LiveSession session,
        Mock<ITeamBoardBroadcaster> broadcaster)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        return new BroadcastTeamBoardNotificationHandler(
            repository.Object,
            broadcaster.Object,
            new FixedTimeProvider(Now));
    }

    // Active treasure-hunt session (first substage is TreasureHunt) with one or more associated teams.
    private static LiveSession CreateActiveTreasureHunt(out IReadOnlyList<Guid> teamIds, int teamCount = 1)
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt(
            $"SES-{Guid.NewGuid():N}"[..12],
            "Treasure Session",
            45,
            Now.AddHours(1));

        var ids = new List<Guid>();
        for (var index = 0; index < teamCount; index++)
        {
            var team = session.AssociateTeam(Guid.NewGuid(), $"Team {index + 1}", $"T-{index + 1:00}", 4);
            ids.Add(team.TeamId);
        }

        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, Now.AddMinutes(-1), policy);

        teamIds = ids;
        return session;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
