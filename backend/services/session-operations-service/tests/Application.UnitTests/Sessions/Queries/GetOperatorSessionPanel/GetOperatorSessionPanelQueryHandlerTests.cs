using System.Reflection;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.Queries.GetOperatorSessionPanel;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetOperatorSessionPanel;

public sealed class GetOperatorSessionPanelQueryHandlerTests
{
    private static readonly DateTimeOffset ScheduledAt = new(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActiveAt = ScheduledAt.AddMinutes(2);

    [Fact]
    public async Task Handle_WhenAssignedOperator_ReturnsSessionStateAndOrderedTeamProgress()
    {
        var session = CreateActiveTreasureHuntSession();
        var observedAt = ActiveAt.AddSeconds(15);
        var resolver = new Mock<ISessionAdministrationAccessResolver>();
        resolver
            .Setup(r => r.GetAuthorizedSessionAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var handler = new GetOperatorSessionPanelQueryHandler(
            resolver.Object,
            new FixedTimeProvider(observedAt));

        var result = await handler.Handle(
            new GetOperatorSessionPanelQuery(session.LiveSessionId),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.State.Should().Be(nameof(SessionState.Active));
        result.Timer.SessionState.Should().Be(nameof(SessionState.Active));
        result.TeamProgress.Should().HaveCount(2);
        result.TeamProgress.Select(team => team.TeamCode).Should().Equal("A-01", "B-02");
        result.TeamProgress.Select(team => team.Score).Should().OnlyContain(score => score == 0);
        result.TeamProgress.Select(team => team.ActiveSubstage).Should().NotContainNulls();
        result.TeamProgress.Select(team => team.ActiveSubstage!.TotalActiveTargets).Should().OnlyContain(count => count == 1);
        result.TeamProgress.Select(team => team.ActiveSubstage!.ResolvedTargets).Should().OnlyContain(count => count == 0);
        result.TeamProgress.Select(team => team.ActiveSubstage!.Targets).Should().OnlyContain(targets =>
            targets.Count == 1 &&
            targets[0].Name == "Main Exhibit" &&
            targets[0].SequenceOrder == 1 &&
            !targets[0].HasHiddenClue);

        resolver.Verify(
            r => r.GetAuthorizedSessionAsync(session.LiveSessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNonOwningOperator_ThrowsForbiddenAccessException()
    {
        var liveSessionId = Guid.NewGuid();
        var resolver = new Mock<ISessionAdministrationAccessResolver>();
        resolver
            .Setup(r => r.GetAuthorizedSessionAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException());
        var handler = new GetOperatorSessionPanelQueryHandler(
            resolver.Object,
            new FixedTimeProvider(ScheduledAt));

        var act = async () => await handler.Handle(
            new GetOperatorSessionPanelQuery(liveSessionId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public void Query_IsRestrictedToOperatorRole()
    {
        var authorizeAttribute = typeof(GetOperatorSessionPanelQuery)
            .GetCustomAttribute<AuthorizeAttribute>();

        authorizeAttribute.Should().NotBeNull();
        authorizeAttribute!.Roles.Should().Be("Operator");
    }

    [Fact]
    public void PanelDto_ContainsOnlyStateTimerAndAllTeamsProgress()
    {
        var rootProperties = typeof(OperatorSessionPanelDto)
            .GetProperties()
            .Select(property => property.Name);
        var teamProperties = typeof(OperatorTeamProgressDto)
            .GetProperties()
            .Select(property => property.Name);

        rootProperties.Should().BeEquivalentTo("LiveSessionId", "State", "Timer", "TeamProgress");
        teamProperties.Should().BeEquivalentTo("TeamId", "TeamCode", "DisplayName", "Score", "ReleasedClueCount", "ActiveSubstage");
        teamProperties.Should().NotContain(new[] { "Rank", "Winner", "Penalty", "ScoreLedger" });
    }

    private static LiveSession CreateActiveTreasureHuntSession()
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt(scheduledAt: ScheduledAt);
        session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-02", 4);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, ActiveAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, ActiveAt, policy);

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
