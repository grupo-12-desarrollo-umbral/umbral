using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

// HU-36A X.1: LiveSession.ProjectActiveQuestionAnsweredStatus() — the pre-close answered/not-answered
// projection over the active synchronized trivia question.
public sealed class LiveSessionAnsweredMonitorTests
{
    private static readonly DateTimeOffset ActiveAt = new(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);

    // Answered path: a team with an accepted submission for the active (substage, sequence) is answered
    // with its SubmittedAt; teams with none render not-answered.
    [Fact]
    public void ProjectActiveQuestionAnsweredStatus_MarksAnsweredTeamAndLeavesOthersNotAnswered()
    {
        var session = ActivateTriviaSessionWithTwoTeams(out var answeredTeam, out var pendingTeam);
        session.ActivateQuestion(0, ActiveAt);
        var submittedAt = ActiveAt.AddSeconds(4);
        session.RegisterTriviaAnswer(answeredTeam.TeamId, selectedOptionSequenceOrder: 1, Guid.NewGuid(), submittedAt);

        var snapshot = session.ProjectActiveQuestionAnsweredStatus();

        snapshot.SubstageSnapshotId.Should().Be(session.ActiveSubstageId!.Value);
        snapshot.QuestionSequenceOrder.Should().Be(1);

        var answered = snapshot.TeamStatuses.Single(status => status.TeamId == answeredTeam.TeamId);
        answered.Answered.Should().BeTrue();
        answered.AnsweredAt.Should().Be(submittedAt);

        var pending = snapshot.TeamStatuses.Single(status => status.TeamId == pendingTeam.TeamId);
        pending.Answered.Should().BeFalse();
        pending.AnsweredAt.Should().BeNull();
    }

    // Not-answered path: with no accepted submission every team renders not-answered.
    [Fact]
    public void ProjectActiveQuestionAnsweredStatus_WhenNoTeamAnswered_RendersEveryTeamNotAnswered()
    {
        var session = ActivateTriviaSessionWithTwoTeams(out _, out _);
        session.ActivateQuestion(0, ActiveAt);

        var snapshot = session.ProjectActiveQuestionAnsweredStatus();

        snapshot.TeamStatuses.Should().HaveCount(2);
        snapshot.TeamStatuses.Should().OnlyContain(status => !status.Answered && status.AnsweredAt == null);
    }

    // Rejection path: an active trivia substage but no question activated → no active trivia question.
    [Fact]
    public void ProjectActiveQuestionAnsweredStatus_WhenNoQuestionActivated_Throws()
    {
        var session = ActivateTriviaSessionWithTwoTeams(out _, out _);

        var act = () => session.ProjectActiveQuestionAnsweredStatus();

        act.Should().Throw<TriviaAnswerRequiresActiveQuestionException>();
    }

    // Rejection path: no active trivia substage at all (session still Scheduled) → rejects.
    [Fact]
    public void ProjectActiveQuestionAnsweredStatus_WhenNoActiveTriviaSubstage_Throws()
    {
        var session = LiveSessionFactory.CreateScheduledTrivia();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);

        var act = () => session.ProjectActiveQuestionAnsweredStatus();

        act.Should().Throw<TriviaAnswerRequiresTriviaSubstageException>();
    }

    // No-leak guarantee at the aggregate boundary: neither the snapshot nor its cells expose the option/
    // correctness/score even when a team has answered.
    [Fact]
    public void ProjectActiveQuestionAnsweredStatus_ProjectionExposesNoOptionCorrectnessOrScore()
    {
        var session = ActivateTriviaSessionWithTwoTeams(out var answeredTeam, out _);
        session.ActivateQuestion(0, ActiveAt);
        session.RegisterTriviaAnswer(answeredTeam.TeamId, selectedOptionSequenceOrder: 1, Guid.NewGuid(), ActiveAt.AddSeconds(4));

        var snapshot = session.ProjectActiveQuestionAnsweredStatus();

        var exposedNames = snapshot.GetType().GetProperties().Select(property => property.Name)
            .Concat(snapshot.TeamStatuses.SelectMany(status => status.GetType().GetProperties().Select(property => property.Name)));

        exposedNames.Should().NotContain(name =>
            name.Contains("Option", StringComparison.Ordinal) ||
            name.Contains("Correct", StringComparison.Ordinal) ||
            name.Contains("Score", StringComparison.Ordinal) ||
            name.Contains("Point", StringComparison.Ordinal));
    }

    private static LiveSession ActivateTriviaSessionWithTwoTeams(out Team answeredTeam, out Team pendingTeam)
    {
        var session = LiveSessionFactory.CreateScheduledTrivia();
        answeredTeam = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        pendingTeam = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);

        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, ActiveAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, ActiveAt, policy);
        return session;
    }
}
