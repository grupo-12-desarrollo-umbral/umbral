using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class LiveSessionOperatorPanelTests
{
    private static readonly DateTimeOffset ActiveAt = new(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);

    [Fact]
    public void ProjectOperatorSessionPanel_CarriesSessionStateAndEveryTeamOrderedWithZeroDefaultScore()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        session.AssociateTeam(Guid.NewGuid(), "Charlie", "C-01", 4);

        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, ActiveAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, ActiveAt, policy);
        session.MoveTo(SessionState.Paused, ActiveAt.AddMinutes(2), policy);

        var panel = session.ProjectOperatorSessionPanel(ActiveAt.AddMinutes(2));

        panel.LiveSessionId.Should().Be(session.LiveSessionId);
        panel.State.Should().Be(SessionState.Paused);
        panel.TeamProgress.Select(progress => progress.TeamCode).Should().Equal("A-01", "B-01", "C-01");
        panel.TeamProgress.Should().OnlyContain(progress => progress.CurrentScore == 0);
    }

    [Fact]
    public void ProjectOperatorSessionPanel_TreasureHunt_ProgressCountsActiveTargetsNotClues()
    {
        var session = LiveSessionFactory.CreateScheduledMultiTargetTreasureHunt();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);

        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, ActiveAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, ActiveAt, policy);

        var panel = session.ProjectOperatorSessionPanel(ActiveAt);

        panel.TeamProgress.Should().HaveCount(2);
        panel.TeamProgress.Should().OnlyContain(progress => progress.ActiveSubstageContext != null);

        var sharedContext = panel.TeamProgress[0].ActiveSubstageContext;

        sharedContext.Should().NotBeNull();
        sharedContext.Should().BeSameAs(panel.TeamProgress[1].ActiveSubstageContext);
        sharedContext!.PlayMode.Should().Be(SubstagePlayMode.TreasureHunt);
        sharedContext.TotalActiveTargets.Should().Be(3);
        sharedContext.ResolvedTargets.Should().Be(0);
        sharedContext.Targets.Select(target => (target.Name, target.SequenceOrder, target.HasHiddenClue))
            .Should().Equal(
                ("Main Exhibit", 1, false),
                ("Main Exhibit", 2, true),
                ("Main Exhibit", 3, false));
        sharedContext.Targets.Select(target => target.TargetSnapshotId)
            .Should().Equal(session.MissionRuntimeSnapshot.TargetSnapshots
                .OrderBy(target => target.SequenceOrder)
                .Select(target => target.TargetSnapshotId));
    }

    [Fact]
    public void ProjectOperatorSessionPanel_TriviaTeams_CarryActiveQuestionAndTimerContext()
    {
        var session = LiveSessionFactory.CreateScheduledTrivia();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);

        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, ActiveAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, ActiveAt, policy);
        session.ActivateQuestion(0, ActiveAt);

        var observedAt = ActiveAt.AddSeconds(10);
        var panel = session.ProjectOperatorSessionPanel(observedAt);

        panel.TimerSnapshot.Should().BeEquivalentTo(session.GetAuthoritativeSessionTimerSnapshot(observedAt));

        var sharedContext = panel.TeamProgress[0].ActiveSubstageContext;

        sharedContext.Should().NotBeNull();
        sharedContext.Should().BeSameAs(panel.TeamProgress[1].ActiveSubstageContext);
        sharedContext!.PlayMode.Should().Be(SubstagePlayMode.Trivia);
        sharedContext.TotalActiveTargets.Should().Be(0);
        sharedContext.ResolvedTargets.Should().Be(0);
        sharedContext.ActiveQuestionSequenceOrder.Should().Be(1);
        sharedContext.ActiveQuestionTimeLimitSeconds.Should().Be(30);
        sharedContext.Targets.Should().BeEmpty();
    }

    [Fact]
    public void ProjectOperatorSessionPanel_WhenNoActiveSubstage_HasNullActiveContextForEveryTeam()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);

        var panel = session.ProjectOperatorSessionPanel(ActiveAt);

        panel.State.Should().Be(SessionState.Scheduled);
        panel.TeamProgress.Should().OnlyContain(progress => progress.ActiveSubstageContext == null);
    }

    [Fact]
    public void OperatorSessionPanelSnapshot_ExposesNoLedgerRankingOrWinnerProperties()
    {
        typeof(umbral_backend.Domain.ValueObjects.OperatorSessionPanelSnapshot)
            .GetProperties()
            .Select(property => property.Name)
            .Should()
            .NotContain(name =>
                name.Contains("Rank", StringComparison.Ordinal) ||
                name.Contains("Ledger", StringComparison.Ordinal) ||
                name.Contains("Penalty", StringComparison.Ordinal) ||
                name.Contains("Winner", StringComparison.Ordinal));

        typeof(umbral_backend.Domain.ValueObjects.OperatorTeamProgress)
            .GetProperties()
            .Select(property => property.Name)
            .Should()
            .NotContain(name =>
                name.Contains("Rank", StringComparison.Ordinal) ||
                name.Contains("Ledger", StringComparison.Ordinal) ||
                name.Contains("Penalty", StringComparison.Ordinal) ||
                name.Contains("Winner", StringComparison.Ordinal));
    }
}
