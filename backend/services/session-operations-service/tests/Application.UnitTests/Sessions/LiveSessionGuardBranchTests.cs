using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions;

// Drives the LiveSession aggregate guards the happy-path command tests skip: duplicate team code /
// association during scheduling, and the trivia-answer domain gates (first-write-wins and the
// answer window) that mirror the application validation chain.
public sealed class LiveSessionGuardBranchTests
{
    [Fact]
    public void AssociateTeam_DuplicateReferenceId_Throws()
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia();
        var referenceId = Guid.NewGuid();
        session.AssociateTeam(referenceId, "Alpha", "A-01", 4);

        var act = () => session.AssociateTeam(referenceId, "Beta", "B-02", 4);

        act.Should().Throw<DuplicateTeamAssociationInSessionException>();
    }

    [Fact]
    public void AssociateTeam_DuplicateTeamCode_Throws()
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "SAME-CODE", 4);

        var act = () => session.AssociateTeam(Guid.NewGuid(), "Beta", "SAME-CODE", 4);

        act.Should().Throw<DuplicateTeamCodeInSessionException>();
    }

    [Fact]
    public void RegisterTriviaAnswer_SecondForSameTeamAndQuestion_ThrowsDuplicate()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out _);
        session.RegisterTriviaAnswer(teamId, 1, Guid.NewGuid(), LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(3));

        var act = () => session.RegisterTriviaAnswer(teamId, 2, Guid.NewGuid(), LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(5));

        act.Should().Throw<DuplicateTriviaAnswerException>();
    }

    [Fact]
    public void RegisterTriviaAnswer_AfterWindowCloses_ThrowsLate()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out _);

        // The single question has a 30s window; 31s in, the window is closed.
        var act = () => session.RegisterTriviaAnswer(teamId, 1, Guid.NewGuid(), LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(31));

        act.Should().Throw<LateTriviaAnswerException>();
    }

    [Fact]
    public void RegisterTriviaAnswer_OnTreasureHuntSubstage_ThrowsRequiresTriviaSubstage()
    {
        // An Active treasure-hunt session has an active (non-trivia) substage, so the state gate admits
        // the write but the active-question resolution rejects the non-trivia substage (L392 second arm).
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt();
        var policy = new SessionStateTransitionPolicy();
        var at = LiveSessionTestFactory.TriviaQuestionActivatedAt;
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        session.MoveTo(SessionState.Preparing, at.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, at.AddSeconds(-30), policy);

        var act = () => session.RegisterTriviaAnswer(team.TeamId, 1, Guid.NewGuid(), at.AddSeconds(1));

        act.Should().Throw<TriviaAnswerRequiresTriviaSubstageException>();
    }

    [Fact]
    public void RegisterTriviaAnswer_AfterTimerMarkedExpired_ThrowsLateViaExpiredFlag()
    {
        // Mark the window expired without closing the question, so the active-question is still resolved
        // and EnsureAnswerWindowOpen rejects via the `_questionTimerExpiredAt.HasValue` arm (L409), not
        // the remaining-time arm the AfterWindowCloses test exercises.
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out var teamId, out _);
        session.MarkQuestionTimerExpiredIfElapsed(LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(31));

        var act = () => session.RegisterTriviaAnswer(teamId, 1, Guid.NewGuid(), LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(20));

        act.Should().Throw<LateTriviaAnswerException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void ActivateQuestion_IndexOutOfRange_Throws(int questionIndex)
    {
        // Single-question session in Active with no question yet active → both the negative and the
        // past-the-end arms of the range guard (L701) reject.
        var session = ActiveTriviaNoQuestion();

        var act = () => session.ActivateQuestion(questionIndex, LiveSessionTestFactory.TriviaQuestionActivatedAt);

        act.Should().Throw<QuestionIndexOutOfRangeException>();
    }

    [Fact]
    public void ActivateQuestion_WhenNotActiveState_Throws()
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia();

        var act = () => session.ActivateQuestion(0, LiveSessionTestFactory.TriviaQuestionActivatedAt);

        act.Should().Throw<QuestionActivationRequiresActiveSessionException>();
    }

    [Fact]
    public void ActivateQuestion_WhenQuestionAlreadyActive_Throws()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out _, out _);

        var act = () => session.ActivateQuestion(0, LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(1));

        act.Should().Throw<QuestionAlreadyActiveException>();
    }

    [Fact]
    public void GetAuthoritativeSessionTimerSnapshot_WhileWindowOpen_IsAdvancing()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out _, out _);

        var snapshot = session.GetAuthoritativeSessionTimerSnapshot(LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(5));

        snapshot.IsAdvancing.Should().BeTrue();
        snapshot.RemainingDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void GetAuthoritativeSessionTimerSnapshot_AfterWindowElapsed_IsExpiredAndNotAdvancing()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out _, out _);

        // 40s past a 30s window with no expiry flag yet → the `remaining <= Zero` arm of L589 marks it
        // expired and clamps the snapshot.
        var snapshot = session.GetAuthoritativeSessionTimerSnapshot(LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(40));

        snapshot.IsAdvancing.Should().BeFalse();
        snapshot.RemainingDuration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void MarkQuestionTimerExpiredIfElapsed_BeforeWindowCloses_StaysAdvancing()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out _, out _);

        // remaining > Zero → early-return advancing snapshot (L614) without setting the expiry flag.
        var snapshot = session.MarkQuestionTimerExpiredIfElapsed(LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(10));

        snapshot.IsAdvancing.Should().BeTrue();
    }

    [Fact]
    public void MarkQuestionTimerExpiredIfElapsed_AfterWindow_FreezesThenStaysFrozenOnRepeat()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out _, out _);

        var first = session.MarkQuestionTimerExpiredIfElapsed(LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(31));
        first.RemainingDuration.Should().Be(TimeSpan.Zero);

        // Second call takes the `_questionTimerExpiredAt.HasValue` short-circuit in
        // CalculateAdvancingQuestionTimerRemaining (L663) and the already-expired arm of the ??= (L621).
        var second = session.MarkQuestionTimerExpiredIfElapsed(LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(45));
        second.RemainingDuration.Should().Be(TimeSpan.Zero);
        second.IsAdvancing.Should().BeFalse();
    }

    [Fact]
    public void ResumeAfterPauseBeyondWindow_TreatsTimerAsExpired()
    {
        // Pause after the window elapsed freezes+expires the timer; resuming (re-entering Active) hits the
        // expired arm of ResumeQuestionTimer (L633/637) rather than re-arming the countdown.
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out _, out _);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Paused, LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(31), policy);

        session.MoveTo(SessionState.Active, LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(32), policy);

        var snapshot = session.GetAuthoritativeSessionTimerSnapshot(LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(33));
        snapshot.IsAdvancing.Should().BeFalse();
        snapshot.RemainingDuration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void SelectTeam_SwitchingTeams_ReleasesOldSlotAndAssignsNew()
    {
        // Pre-start switch: the participant already holds an active slot in team A, so picking team B
        // drives Team.ReleaseParticipant's matching-active-member arm on A (freeing the slot) and the
        // assignment on B.
        var session = LiveSessionTestFactory.CreateScheduledTrivia();
        var refA = Guid.NewGuid();
        var refB = Guid.NewGuid();
        var teamA = session.AssociateTeam(refA, "Alpha", "A-01", 4);
        var teamB = session.AssociateTeam(refB, "Beta", "B-02", 4);
        var policy = new OpenTeamSelectionPolicy();
        var authorized = new HashSet<Guid> { refA, refB };
        var participant = Guid.NewGuid();
        var at = LiveSessionTestFactory.TriviaQuestionActivatedAt;

        session.SelectTeam(participant, "Alice", teamA.TeamId, authorized, at, policy);
        session.SelectTeam(participant, "Alice", teamB.TeamId, authorized, at.AddSeconds(1), policy);

        teamA.ActiveMemberCount.Should().Be(0);
        teamB.ActiveMemberCount.Should().Be(1);
    }

    [Fact]
    public void SelectTeam_RepickingCurrentTeam_IsNoOp()
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia();
        var refA = Guid.NewGuid();
        var teamA = session.AssociateTeam(refA, "Alpha", "A-01", 4);
        var policy = new OpenTeamSelectionPolicy();
        var authorized = new HashSet<Guid> { refA };
        var participant = Guid.NewGuid();
        var at = LiveSessionTestFactory.TriviaQuestionActivatedAt;
        session.SelectTeam(participant, "Alice", teamA.TeamId, authorized, at, policy);

        // Re-picking the same team returns early (currentTeam.TeamId == target.TeamId) without releasing.
        session.SelectTeam(participant, "Alice", teamA.TeamId, authorized, at.AddSeconds(1), policy);

        teamA.ActiveMemberCount.Should().Be(1);
    }

    [Fact]
    public void SelectTeam_SwitchingOutOfSharedTeam_ReleasesOnlyTheSwitcher()
    {
        // Two participants share team A; when the first switches to B, Team.ReleaseParticipant must scan
        // past the other active member (id-mismatch arm) before freeing only the switcher's slot.
        var session = LiveSessionTestFactory.CreateScheduledTrivia();
        var refA = Guid.NewGuid();
        var refB = Guid.NewGuid();
        var teamA = session.AssociateTeam(refA, "Alpha", "A-01", 4);
        var teamB = session.AssociateTeam(refB, "Beta", "B-02", 4);
        var policy = new OpenTeamSelectionPolicy();
        var authorized = new HashSet<Guid> { refA, refB };
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var at = LiveSessionTestFactory.TriviaQuestionActivatedAt;
        session.SelectTeam(first, "Alice", teamA.TeamId, authorized, at, policy);
        session.SelectTeam(second, "Bob", teamA.TeamId, authorized, at.AddSeconds(1), policy);

        session.SelectTeam(first, "Alice", teamB.TeamId, authorized, at.AddSeconds(2), policy);

        teamA.ActiveMemberCount.Should().Be(1);
        teamB.ActiveMemberCount.Should().Be(1);
    }

    [Fact]
    public void MoveTo_Cancelled_RoutesThroughCancelledStateFactory()
    {
        var session = LiveSessionTestFactory.CreateActiveTriviaWithActiveQuestion(out _, out _);
        var policy = new SessionStateTransitionPolicy();

        session.MoveTo(SessionState.Cancelled, LiveSessionTestFactory.TriviaQuestionActivatedAt.AddSeconds(5), policy, "operator ended");

        session.State.Should().Be(SessionState.Cancelled);
    }

    private static LiveSession ActiveTriviaNoQuestion()
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia();
        var policy = new SessionStateTransitionPolicy();
        var at = LiveSessionTestFactory.TriviaQuestionActivatedAt;
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        session.MoveTo(SessionState.Preparing, at.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, at.AddSeconds(-30), policy);
        return session;
    }
}
