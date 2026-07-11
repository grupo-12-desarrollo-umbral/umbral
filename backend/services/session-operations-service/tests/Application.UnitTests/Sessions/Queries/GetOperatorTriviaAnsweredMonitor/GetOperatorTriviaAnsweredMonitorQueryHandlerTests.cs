using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Queries.GetOperatorTriviaAnsweredMonitor;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetOperatorTriviaAnsweredMonitor;

// HU-36A operator monitor query: the read is a guarded projection. The ownership decision is delegated
// entirely to ISessionAdministrationAccessResolver (the Proxy) — the assigned operator gets the
// per-team answered/not-answered roster, a non-owner is rejected with ForbiddenAccessException, and the
// DTO structurally leaks no option/correctness/score.
public sealed class GetOperatorTriviaAnsweredMonitorQueryHandlerTests
{
    private static readonly DateTimeOffset ActivatedAt = LiveSessionTestFactory.TriviaQuestionActivatedAt;

    [Fact]
    public async Task Handle_WhenAssignedOperator_ReturnsPerTeamAnsweredStatusForActiveQuestion()
    {
        // Two teams associated while Scheduled; only one answers the active question, so the roster
        // carries both an answered and a not-answered cell (not-answered derived from the session's
        // teams, not from list absence).
        var session = LiveSessionTestFactory.CreateScheduledTrivia();
        var policy = new SessionStateTransitionPolicy();
        var answeredTeam = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var notAnsweredTeam = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-02", 4);
        session.MoveTo(SessionState.Preparing, ActivatedAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, ActivatedAt.AddSeconds(-30), policy);
        session.ActivateQuestion(0, ActivatedAt);
        var substageId = session.ActiveSubstageId!.Value;

        var answeredAt = ActivatedAt.AddSeconds(3);
        session.RegisterTriviaAnswer(answeredTeam.TeamId, selectedOptionSequenceOrder: 1,
            submittedByParticipantId: Guid.NewGuid(), answeredAt);

        var handler = CreateHandler(session);

        var result = await handler.Handle(
            new GetOperatorTriviaAnsweredMonitorQuery(session.LiveSessionId),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.SubstageSnapshotId.Should().Be(substageId);
        result.QuestionSequenceOrder.Should().Be(1);
        result.Teams.Should().HaveCount(2);

        var answered = result.Teams.Single(team => team.TeamId == answeredTeam.TeamId);
        answered.Answered.Should().BeTrue();
        answered.AnsweredAt.Should().Be(answeredAt);

        var notAnswered = result.Teams.Single(team => team.TeamId == notAnsweredTeam.TeamId);
        notAnswered.Answered.Should().BeFalse();
        notAnswered.AnsweredAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenNonOwningOperator_ThrowsForbiddenAccessException()
    {
        var liveSessionId = Guid.NewGuid();
        var resolver = new Mock<ISessionAdministrationAccessResolver>();
        resolver
            .Setup(r => r.GetAuthorizedSessionAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException());
        var handler = new GetOperatorTriviaAnsweredMonitorQueryHandler(resolver.Object);

        var act = async () => await handler.Handle(
            new GetOperatorTriviaAnsweredMonitorQuery(liveSessionId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public void MonitorDto_LeaksNoOptionCorrectnessOrScore()
    {
        var rootProperties = typeof(TriviaAnsweredMonitorDto)
            .GetProperties()
            .Select(property => property.Name);
        var teamProperties = typeof(TriviaTeamAnsweredStatusDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        rootProperties.Should().BeEquivalentTo(
            "LiveSessionId", "SubstageSnapshotId", "QuestionSequenceOrder", "Teams");
        teamProperties.Should().NotContain(new[] { "SelectedOptionSequenceOrder", "IsCorrect", "ScoreValue" });
        teamProperties.Should().BeEquivalentTo(
            "TeamId", "TeamCode", "DisplayName", "Answered", "AnsweredAt");
    }

    private static GetOperatorTriviaAnsweredMonitorQueryHandler CreateHandler(LiveSession session)
    {
        var resolver = new Mock<ISessionAdministrationAccessResolver>();
        resolver
            .Setup(r => r.GetAuthorizedSessionAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        return new GetOperatorTriviaAnsweredMonitorQueryHandler(resolver.Object);
    }
}
