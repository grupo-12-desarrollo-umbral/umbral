using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class LiveSessionTests
{
    private readonly JoinPolicy _joinPolicy = new();

    [Fact]
    public void CreateTrivia_WithMismatchedSource_ThrowsException()
    {
        var act = () => LiveSession.CreateTrivia(
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            "abc123",
            "Trivia Session",
            30,
            DateTimeOffset.UtcNow,
            TriviaSessionSnapshotFactory.CreateSingleQuestion());

        act.Should().Throw<SessionSourceDoesNotMatchModeException>();
    }

    [Fact]
    public void Create_WithTriviaModeWithoutSnapshot_ThrowsException()
    {
        var act = () => LiveSession.Create(
            SessionMode.Trivia,
            SessionSource.CreateTriviaQuiz(42),
            "tri-123",
            "Trivia Session",
            30,
            DateTimeOffset.UtcNow);

        act.Should().Throw<TriviaSessionSnapshotRequiredException>();
    }

    [Fact]
    public void CreateTrivia_WithSnapshot_AttachesFixedCopy()
    {
        var snapshot = TriviaSessionSnapshotFactory.CreateSingleQuestion();

        var session = LiveSession.CreateTrivia(
            SessionSource.CreateTriviaQuiz(42),
            "tri-123",
            "Trivia Session",
            30,
            DateTimeOffset.UtcNow,
            snapshot);

        session.SessionMode.Should().Be(SessionMode.Trivia);
        session.Source.SourceTriviaQuizId.Should().Be(42);
        session.TriviaSnapshot.Should().NotBeNull();
        session.TriviaSnapshot!.Questions.Should().ContainSingle();
        session.TriviaSnapshot.QuizTitle.Should().Be("Foundations of Science");
    }

    [Fact]
    public void RegisterTeam_WithDuplicateCode_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        session.RegisterTeam("Alpha", "alpha", 4);

        var act = () => session.RegisterTeam("Beta", "ALPHA", 4);

        act.Should().Throw<DuplicateTeamCodeInSessionException>();
    }

    [Fact]
    public void RegisterTeam_WithExplicitTeamId_PreservesIdentityReferenceTeamId()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var identityReferenceTeamId = Guid.NewGuid();

        var team = session.RegisterTeam(identityReferenceTeamId, "Alpha", "A-01", 4);

        team.TeamId.Should().Be(identityReferenceTeamId);
        team.ReferenceTeamId.Should().BeNull();
        session.Teams.Should().ContainSingle().Which.TeamId.Should().Be(identityReferenceTeamId);
    }

    [Fact]
    public void RegisterTeam_WithEmptyTeamId_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();

        var act = () => session.RegisterTeam(Guid.Empty, "Alpha", "A-01", 4);

        act.Should().Throw<TeamIdentityRequiredException>();
    }

    [Fact]
    public void AssociateTeam_WithRegisteredReference_AddsSessionOwnedTeam()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var referenceTeamId = Guid.NewGuid();

        var team = session.AssociateTeam(referenceTeamId, "Alpha", "A-01", 3);

        session.HasAssociatedTeams.Should().BeTrue();
        session.AssociatedTeamCount.Should().Be(1);
        session.Teams.Should().ContainSingle().Which.Should().Be(team);
        team.TeamId.Should().NotBe(referenceTeamId);
        team.ReferenceTeamId.Should().Be(referenceTeamId);
        team.Capacity.Should().Be(3);
    }

    [Fact]
    public void AssociateTeam_WithDuplicateReference_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var referenceTeamId = Guid.NewGuid();
        session.AssociateTeam(referenceTeamId, "Alpha", "A-01", 3);

        var act = () => session.AssociateTeam(referenceTeamId, "Beta", "B-01", 2);

        act.Should().Throw<DuplicateTeamAssociationInSessionException>();
    }

    [Fact]
    public void AssociateTeam_WhenSessionIsNotScheduled_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var policy = new SessionStateTransitionPolicy();
        session.RegisterTeam("Alpha", "A-01", 4);
        session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow, policy);

        var act = () => session.AssociateTeam(Guid.NewGuid(), "Beta", "B-01", 2);

        act.Should().Throw<TeamAssociationRequiresScheduledSessionException>();
    }

    [Fact]
    public void AdmitParticipant_WhenNewParticipant_CreatesMembership()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.RegisterTeam("Alpha", "A-01", 4);

        var result = session.AdmitParticipant(Guid.NewGuid(), "Nora", team.TeamId, DateTimeOffset.UtcNow, _joinPolicy);

        result.IsReconnect.Should().BeFalse();
        result.Participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
        result.Team.Members.Should().ContainSingle();
    }

    [Fact]
    public void AdmitParticipant_WhenParticipantReconnects_RestoresPresenceInAssignedTeam()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.RegisterTeam("Alpha", "A-01", 4);
        var identityId = Guid.NewGuid();
        var joinedAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var joined = session.AdmitParticipant(identityId, "Nora", team.TeamId, joinedAt, _joinPolicy);
        session.DisconnectParticipant(joined.Participant.SessionParticipantId, joinedAt.AddMinutes(5));
        session.MoveTo(SessionState.Preparing, joinedAt.AddMinutes(6), new SessionStateTransitionPolicy());

        var reconnected = session.AdmitParticipant(identityId, "Nora", team.TeamId, joinedAt.AddMinutes(7), _joinPolicy);

        reconnected.IsReconnect.Should().BeTrue();
        reconnected.Team.TeamId.Should().Be(team.TeamId);
        reconnected.Participant.ParticipantStatus.Should().Be(ParticipantStatus.Active);
    }

    [Fact]
    public void AdmitParticipant_WhenSessionIsActive_RejectsLateJoin()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow, transitionPolicy);
        session.MoveTo(SessionState.Active, DateTimeOffset.UtcNow.AddMinutes(1), transitionPolicy);

        var act = () => session.AdmitParticipant(Guid.NewGuid(), "Late", team.TeamId, DateTimeOffset.UtcNow.AddMinutes(2), _joinPolicy);

        act.Should().Throw<LateJoinNotAllowedException>();
    }

    [Fact]
    public void Create_WithBlankSessionCode_ThrowsException()
    {
        var act = () => LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            " ",
            "Treasure Session",
            30,
            DateTimeOffset.UtcNow);

        act.Should().Throw<LiveSessionCodeRequiredException>();
    }

    [Fact]
    public void Create_WithBlankTitle_ThrowsException()
    {
        var act = () => LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            "ses-123",
            " ",
            30,
            DateTimeOffset.UtcNow);

        act.Should().Throw<LiveSessionTitleRequiredException>();
    }

    [Fact]
    public void OpenJoinContext_AddsPendingJoinContext()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var createdAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);

        var joinContext = session.OpenJoinContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            createdAt,
            createdAt.AddMinutes(5));

        session.JoinContexts.Should().ContainSingle().Which.Should().Be(joinContext);
        joinContext.Status.Should().Be(JoinContextStatus.Pending);
    }

    [Fact]
    public void AssignOperator_WithPositiveUserId_AssignsResponsibleOperatorAndRaisesAuditFact()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var occurredAt = new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);

        session.AssignOperator(27, occurredAt);

        session.AssignedOperatorUserId.Should().Be(27);
        session.DomainEvents.OfType<LiveSessionCreatedEvent>().Should().ContainSingle();

        var assignmentEvent = session.DomainEvents
            .OfType<LiveSessionOperatorAssignedEvent>()
            .Single();

        assignmentEvent.LiveSessionId.Should().Be(session.LiveSessionId);
        assignmentEvent.PreviousOperatorUserId.Should().BeNull();
        assignmentEvent.AssignedOperatorUserId.Should().Be(27);
        assignmentEvent.OccurredAt.Should().Be(occurredAt);
    }

    [Fact]
    public void AssignOperator_WhenReassignedInActiveSession_ReplacesResponsibleOperatorAndRaisesAuditFact()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow, transitionPolicy);
        session.MoveTo(SessionState.Active, DateTimeOffset.UtcNow.AddMinutes(1), transitionPolicy);
        session.AssignOperator(27, DateTimeOffset.UtcNow.AddMinutes(2));
        var reassignedAt = DateTimeOffset.UtcNow.AddMinutes(3);

        session.AssignOperator(31, reassignedAt);

        session.AssignedOperatorUserId.Should().Be(31);

        var reassignmentEvent = session.DomainEvents
            .OfType<LiveSessionOperatorAssignedEvent>()
            .Last();

        reassignmentEvent.PreviousOperatorUserId.Should().Be(27);
        reassignmentEvent.AssignedOperatorUserId.Should().Be(31);
        reassignmentEvent.OccurredAt.Should().Be(reassignedAt);
    }

    [Fact]
    public void AssignOperator_WithNonPositiveUserId_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();

        var act = () => session.AssignOperator(0, DateTimeOffset.UtcNow);

        act.Should().Throw<OperatorUserIdMustBePositiveException>();
    }

    [Fact]
    public void MoveTo_TracksLifecycleTimestampsAndReason()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        var preparingAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var activeAt = preparingAt.AddMinutes(1);
        var pausedAt = activeAt.AddMinutes(5);
        var finishedAt = pausedAt.AddMinutes(3);

        session.MoveTo(SessionState.Preparing, preparingAt, policy, "  ready  ");
        session.MoveTo(SessionState.Active, activeAt, policy);
        session.MoveTo(SessionState.Paused, pausedAt, policy);
        session.MoveTo(SessionState.Finished, finishedAt, policy, "  complete  ");

        session.StartedAt.Should().Be(activeAt);
        session.PausedAt.Should().Be(pausedAt);
        session.EndedAt.Should().Be(finishedAt);
        session.StateReason.Should().Be("complete");
        session.LastStateChangedAt.Should().Be(finishedAt);
    }

    [Fact]
    public void GetAuthoritativeSessionTimerSnapshot_WhenActive_DecrementsFromMaximumTime()
    {
        var session = LiveSessionFactory.CreateScheduledTrivia(maximumTimeMinutes: 10);
        session.RegisterTeam("Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        var preparingAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var activeAt = preparingAt.AddMinutes(1);

        session.MoveTo(SessionState.Preparing, preparingAt, policy);
        session.MoveTo(SessionState.Active, activeAt, policy);

        var snapshot = session.GetAuthoritativeSessionTimerSnapshot(activeAt.AddMinutes(3));

        snapshot.TotalDuration.Should().Be(TimeSpan.FromMinutes(10));
        snapshot.RemainingDuration.Should().Be(TimeSpan.FromMinutes(7));
        snapshot.IsAdvancing.Should().BeTrue();
        snapshot.IsExpired.Should().BeFalse();
        snapshot.AdvancingSince.Should().Be(activeAt);
    }

    [Fact]
    public void GetAuthoritativeSessionTimerSnapshot_WhenPaused_FreezesRemainingTime()
    {
        var session = LiveSessionFactory.CreateScheduledTrivia(maximumTimeMinutes: 10);
        session.RegisterTeam("Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        var preparingAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var activeAt = preparingAt.AddMinutes(1);
        var pausedAt = activeAt.AddMinutes(4);

        session.MoveTo(SessionState.Preparing, preparingAt, policy);
        session.MoveTo(SessionState.Active, activeAt, policy);
        session.MoveTo(SessionState.Paused, pausedAt, policy);

        var snapshot = session.GetAuthoritativeSessionTimerSnapshot(pausedAt.AddMinutes(8));

        snapshot.RemainingDuration.Should().Be(TimeSpan.FromMinutes(6));
        snapshot.IsAdvancing.Should().BeFalse();
        snapshot.IsExpired.Should().BeFalse();
        snapshot.AdvancingSince.Should().BeNull();
    }

    [Fact]
    public void GetAuthoritativeSessionTimerSnapshot_WhenResumed_ContinuesFromFrozenRemainder()
    {
        var session = LiveSessionFactory.CreateScheduledTrivia(maximumTimeMinutes: 10);
        session.RegisterTeam("Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        var preparingAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var firstActiveAt = preparingAt.AddMinutes(1);
        var pausedAt = firstActiveAt.AddMinutes(4);
        var resumedAt = pausedAt.AddMinutes(5);

        session.MoveTo(SessionState.Preparing, preparingAt, policy);
        session.MoveTo(SessionState.Active, firstActiveAt, policy);
        session.MoveTo(SessionState.Paused, pausedAt, policy);
        session.MoveTo(SessionState.Active, resumedAt, policy);

        var snapshot = session.GetAuthoritativeSessionTimerSnapshot(resumedAt.AddMinutes(2));

        snapshot.RemainingDuration.Should().Be(TimeSpan.FromMinutes(4));
        snapshot.IsAdvancing.Should().BeTrue();
        snapshot.AdvancingSince.Should().Be(resumedAt);
    }

    [Fact]
    public void GetAuthoritativeSessionTimerSnapshot_ForReconnect_ReturnsBackendOwnedRemainder()
    {
        var session = LiveSessionFactory.CreateScheduledTrivia(maximumTimeMinutes: 10);
        var team = session.RegisterTeam("Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        var joinedAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var admitted = session.AdmitParticipant(Guid.NewGuid(), "Nora", team.TeamId, joinedAt, _joinPolicy);
        var activeAt = joinedAt.AddMinutes(2);

        session.MoveTo(SessionState.Preparing, joinedAt.AddMinutes(1), policy);
        session.MoveTo(SessionState.Active, activeAt, policy);
        session.DisconnectParticipant(admitted.Participant.SessionParticipantId, activeAt.AddMinutes(3));
        var reconnected = session.AdmitParticipant(
            admitted.Participant.ExternalIdentityId,
            "Nora",
            team.TeamId,
            activeAt.AddMinutes(5),
            _joinPolicy);

        var reconnectSnapshot = session.GetAuthoritativeSessionTimerSnapshot(activeAt.AddMinutes(5));

        reconnected.IsReconnect.Should().BeTrue();
        reconnectSnapshot.RemainingDuration.Should().Be(TimeSpan.FromMinutes(5));
        reconnectSnapshot.IsAdvancing.Should().BeTrue();
    }

    [Fact]
    public void MarkSessionTimerExpiredIfElapsed_WhenActiveTimerReachesZero_MarksExpiryAndStopsAdvancing()
    {
        var session = LiveSessionFactory.CreateScheduledTrivia(maximumTimeMinutes: 10);
        session.RegisterTeam("Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        var preparingAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var activeAt = preparingAt.AddMinutes(1);
        var expiredAt = activeAt.AddMinutes(10);

        session.MoveTo(SessionState.Preparing, preparingAt, policy);
        session.MoveTo(SessionState.Active, activeAt, policy);

        var snapshot = session.MarkSessionTimerExpiredIfElapsed(expiredAt);

        snapshot.RemainingDuration.Should().Be(TimeSpan.Zero);
        snapshot.IsExpired.Should().BeTrue();
        snapshot.IsAdvancing.Should().BeFalse();
        snapshot.ExpiredAt.Should().Be(expiredAt);
        session.IsSessionTimerAdvancing.Should().BeFalse();
    public void MoveTo_ActiveWithOnlyNonAssociatedRuntimeTeam_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        session.RegisterTeam("Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();

        session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow, policy);

        var act = () => session.MoveTo(SessionState.Active, DateTimeOffset.UtcNow.AddMinutes(1), policy);

        act.Should().Throw<LiveSessionRequiresAtLeastOneTeamException>();
    }
}
