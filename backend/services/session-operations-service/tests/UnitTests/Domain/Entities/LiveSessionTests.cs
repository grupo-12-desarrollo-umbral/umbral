using System.Reflection;
using umbral_backend.Domain.Common;
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
    public void Create_WithMissionSource_SetsScheduledStateAndOwnsRuntimeSnapshot()
    {
        var runtimeSnapshot = MissionRuntimeSnapshotFactory.CreateTriviaSnapshot();

        var session = LiveSession.Create(
            SessionSource.Create(runtimeSnapshot.SourceMissionId),
            "tri-123",
            "Trivia Session",
            30,
            DateTimeOffset.UtcNow,
            runtimeSnapshot);

        session.State.Should().Be(SessionState.Scheduled);
        session.MissionRuntimeSnapshot.Should().Be(runtimeSnapshot);
        session.MissionRuntimeSnapshot.TriviaQuestionSnapshots.Should().ContainSingle();
    }

    [Fact]
    public void Create_WithEmptyMissionSource_ThrowsException()
    {
        var runtimeSnapshot = MissionRuntimeSnapshotFactory.CreateTreasureHuntSnapshot();

        var act = () => LiveSession.Create(
            SessionSource.Create(Guid.Empty),
            "ses-123",
            "Mission Session",
            30,
            DateTimeOffset.UtcNow,
            runtimeSnapshot);

        act.Should().Throw<SessionSourceEntityRequiredException>();
    }

    [Fact]
    public void Create_WithAssignedOperator_PreservesOptionalAssignment()
    {
        var runtimeSnapshot = MissionRuntimeSnapshotFactory.CreateTriviaSnapshot();

        var session = LiveSession.Create(
            SessionSource.Create(runtimeSnapshot.SourceMissionId),
            "tri-123",
            "Trivia Session",
            30,
            DateTimeOffset.UtcNow,
            runtimeSnapshot,
            assignedOperatorUserId: 27);

        session.AssignedOperatorUserId.Should().Be(27);
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
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow, policy);

        var act = () => session.AssociateTeam(Guid.NewGuid(), "Beta", "B-01", 2);

        act.Should().Throw<TeamAssociationRequiresScheduledSessionException>();
    }

    [Theory]
    [InlineData(SessionState.Active)]
    [InlineData(SessionState.Paused)]
    [InlineData(SessionState.Cancelled)]
    public void AssociateTeam_AfterLeavingScheduled_ThrowsException(SessionState reachedState)
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var policy = new SessionStateTransitionPolicy();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        DriveTo(session, reachedState, policy);

        var act = () => session.AssociateTeam(Guid.NewGuid(), "Beta", "B-01", 2);

        act.Should().Throw<TeamAssociationRequiresScheduledSessionException>();
    }

    [Fact]
    public void MoveTo_OverAllowedEdge_RaisesSessionStateChangedEvent()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var occurredAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);

        session.MoveTo(
            SessionState.Preparing,
            occurredAt,
            new SessionStateTransitionPolicy(),
            "ready",
            responsibleUserId: 27);

        session.State.Should().Be(SessionState.Preparing);

        var stateEvent = session.DomainEvents.OfType<SessionStateChangedEvent>().Single();
        stateEvent.LiveSessionId.Should().Be(session.LiveSessionId);
        stateEvent.PreviousState.Should().Be(SessionState.Scheduled);
        stateEvent.CurrentState.Should().Be(SessionState.Preparing);
        stateEvent.ChangedAt.Should().Be(occurredAt);
        stateEvent.ResponsibleUserId.Should().Be(27);
        stateEvent.Reason.Should().Be("ready");
        stateEvent.ActorType.Should().Be(SessionEventActorType.Operator);

        var auditRecord = session.SessionEvents.Should().ContainSingle().Subject;
        auditRecord.LiveSessionId.Should().Be(session.LiveSessionId);
        auditRecord.OccurredAt.Should().Be(occurredAt);
        auditRecord.ActorType.Should().Be(SessionEventActorType.Operator);
        auditRecord.ActorId.Should().Be(27);
        auditRecord.PayloadSummary.Should().Be("Scheduled→Preparing: ready");
    }

    [Fact]
    public void MoveTo_OverRejectedEdge_ThrowsAndRaisesNoStateChangedEvent()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();

        var act = () => session.MoveTo(SessionState.Finished, DateTimeOffset.UtcNow, new SessionStateTransitionPolicy());

        act.Should().Throw<InvalidSessionStateTransitionException>();
        session.State.Should().Be(SessionState.Scheduled);
        session.DomainEvents.OfType<SessionStateChangedEvent>().Should().BeEmpty();
        session.SessionEvents.Should().BeEmpty();
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

    // A first join through AdmitParticipant is a self-assignment, so it clears the same authorized set as
    // SelectTeam when the caller supplies one.
    [Fact]
    public void AdmitParticipant_WhenFirstJoinTargetsTeamOutsideAuthorizedSet_ThrowsTeamNotInAuthorizedSet()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var alphaReferenceTeamId = Guid.NewGuid();
        session.AssociateTeam(alphaReferenceTeamId, "Alpha", "A-01", 4);
        var bravo = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);

        var act = () => session.AdmitParticipant(
            Guid.NewGuid(),
            "Mallory",
            bravo.TeamId,
            DateTimeOffset.UtcNow,
            _joinPolicy,
            new OpenTeamSelectionPolicy(),
            new HashSet<Guid> { alphaReferenceTeamId });

        act.Should().Throw<TeamNotInAuthorizedSetException>();
        session.Participants.Should().BeEmpty();
        bravo.Members.Should().BeEmpty();
    }

    [Fact]
    public void AdmitParticipant_WhenFirstJoinTargetsWhitelistedTeam_CreatesMembership()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var alphaReferenceTeamId = Guid.NewGuid();
        var alpha = session.AssociateTeam(alphaReferenceTeamId, "Alpha", "A-01", 4);

        var result = session.AdmitParticipant(
            Guid.NewGuid(),
            "Nora",
            alpha.TeamId,
            DateTimeOffset.UtcNow,
            _joinPolicy,
            new OpenTeamSelectionPolicy(),
            new HashSet<Guid> { alphaReferenceTeamId });

        result.IsReconnect.Should().BeFalse();
        result.Team.Members.Should().ContainSingle();
    }

    // An empty authorized set is "unassigned => every attached team", never "denied".
    [Fact]
    public void AdmitParticipant_WhenFirstJoinByUnassignedParticipant_CreatesMembership()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);

        var result = session.AdmitParticipant(
            Guid.NewGuid(),
            "Nora",
            team.TeamId,
            DateTimeOffset.UtcNow,
            _joinPolicy,
            new OpenTeamSelectionPolicy(),
            new HashSet<Guid>());

        result.IsReconnect.Should().BeFalse();
        result.Team.Members.Should().ContainSingle();
    }

    // Policy supplied, authorized set omitted: the omitted set carries the same "unassigned" meaning as an
    // empty one, so the caller is admitted rather than refused for want of a whitelist.
    [Fact]
    public void AdmitParticipant_WhenFirstJoinWithPolicyButNoAuthorizedSet_TreatsCallerAsUnassigned()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);

        var result = session.AdmitParticipant(
            Guid.NewGuid(),
            "Nora",
            team.TeamId,
            DateTimeOffset.UtcNow,
            _joinPolicy,
            new OpenTeamSelectionPolicy());

        result.IsReconnect.Should().BeFalse();
        result.Team.Members.Should().ContainSingle();
    }

    // A returning participant is bound to their assigned team by EnsureCanReconnect, so a whitelist that no
    // longer lists it must not strand them mid-session.
    [Fact]
    public void AdmitParticipant_WhenReconnectingToAssignedTeamOutsideAuthorizedSet_StillReconnects()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var identityId = Guid.NewGuid();
        var joinedAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        session.AdmitParticipant(identityId, "Nora", team.TeamId, joinedAt, _joinPolicy);

        var reconnected = session.AdmitParticipant(
            identityId,
            "Nora",
            team.TeamId,
            joinedAt.AddMinutes(5),
            _joinPolicy,
            new OpenTeamSelectionPolicy(),
            new HashSet<Guid> { Guid.NewGuid() });

        reconnected.IsReconnect.Should().BeTrue();
        reconnected.Team.TeamId.Should().Be(team.TeamId);
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
        var runtimeSnapshot = MissionRuntimeSnapshotFactory.CreateTreasureHuntSnapshot(maximumTimeMinutes: 30);

        var act = () => LiveSession.Create(
            SessionSource.Create(runtimeSnapshot.SourceMissionId),
            " ",
            "Treasure Session",
            30,
            DateTimeOffset.UtcNow,
            runtimeSnapshot);

        act.Should().Throw<LiveSessionCodeRequiredException>();
    }

    [Fact]
    public void Create_WithBlankTitle_ThrowsException()
    {
        var runtimeSnapshot = MissionRuntimeSnapshotFactory.CreateTreasureHuntSnapshot(maximumTimeMinutes: 30);

        var act = () => LiveSession.Create(
            SessionSource.Create(runtimeSnapshot.SourceMissionId),
            "ses-123",
            " ",
            30,
            DateTimeOffset.UtcNow,
            runtimeSnapshot);

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

        session.MoveTo(SessionState.Preparing, preparingAt, policy, "  ready  ");
        session.MoveTo(SessionState.Active, activeAt, policy);
        session.MoveTo(SessionState.Paused, pausedAt, policy, "  taking a break  ");

        session.StartedAt.Should().Be(activeAt);
        session.PausedAt.Should().Be(pausedAt);
        session.EndedAt.Should().BeNull();
        session.StateReason.Should().Be("taking a break");
        session.LastStateChangedAt.Should().Be(pausedAt);
    }

    // Finished is reached ONLY via SessionCompletion, never via the manual/generic MoveTo path used
    // by the Operator PATCH endpoint (Issue: manual Active/Paused -> Finished must be rejected).
    // EndedAt tracking for the automatic path is covered by
    // CompleteActiveSubstageAndAdvance_WhenNoNextSubstage_FinishesViaSessionCompletion.
    [Theory]
    [InlineData(SessionState.Active)]
    [InlineData(SessionState.Paused)]
    public void MoveTo_ManualToFinished_ThrowsAndRaisesNoStateChangedEvent(SessionState from)
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        var now = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        session.MoveTo(SessionState.Preparing, now, policy);
        session.MoveTo(SessionState.Active, now.AddMinutes(1), policy);
        if (from == SessionState.Paused)
        {
            session.MoveTo(SessionState.Paused, now.AddMinutes(2), policy);
        }

        session.ClearDomainEvents();
        var act = () => session.MoveTo(SessionState.Finished, now.AddMinutes(3), policy, responsibleUserId: 27);

        act.Should().Throw<InvalidSessionStateTransitionException>();
        session.State.Should().Be(from);
        session.DomainEvents.OfType<SessionStateChangedEvent>().Should().BeEmpty();
    }

    // HU-22 / OD-1/2/3: authoritative remaining time = the active trivia-question window;
    // no advancing countdown when no trivia question is active.
    [Fact]
    public void GetAuthoritativeSessionTimerSnapshot_WhenTriviaQuestionActive_TracksActiveQuestionWindow()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, activatedAt);

        var snapshot = session.GetAuthoritativeSessionTimerSnapshot(activatedAt.AddSeconds(10));

        // 30s question, 10s elapsed -> the authoritative remaining IS the question window
        snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(30));
        snapshot.RemainingDuration.Should().Be(TimeSpan.FromSeconds(20));
        snapshot.IsAdvancing.Should().BeTrue();
        snapshot.AdvancingSince.Should().Be(activatedAt);
    }

    // OD-1: an Active session with a 10-minute MaximumTime but no active question has NO
    // advancing countdown — proves the whole-session MaximumTime countdown is gone.
    [Fact]
    public void GetAuthoritativeSessionTimerSnapshot_WhenNoQuestionActive_HasNoAdvancingCountdown()
    {
        var session = LiveSessionFactory.CreateScheduledTrivia(maximumTimeMinutes: 10);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        var preparingAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var activeAt = preparingAt.AddMinutes(1);

        session.MoveTo(SessionState.Preparing, preparingAt, policy);
        session.MoveTo(SessionState.Active, activeAt, policy);

        var snapshot = session.GetAuthoritativeSessionTimerSnapshot(activeAt.AddMinutes(3));

        snapshot.TotalDuration.Should().Be(TimeSpan.Zero);
        snapshot.RemainingDuration.Should().Be(TimeSpan.Zero);
        snapshot.IsAdvancing.Should().BeFalse();
    }

    [Fact]
    public void GetAuthoritativeSessionTimerSnapshot_WhenSessionHasNoActiveSubstage_HasNoAdvancingCountdown()
    {
        var session = LiveSessionFactory.CreateScheduledTrivia(maximumTimeMinutes: 10);
        var observedAt = new DateTimeOffset(2026, 6, 3, 10, 3, 0, TimeSpan.Zero);

        var snapshot = session.GetAuthoritativeSessionTimerSnapshot(observedAt);

        snapshot.TotalDuration.Should().Be(TimeSpan.Zero);
        snapshot.RemainingDuration.Should().Be(TimeSpan.Zero);
        snapshot.IsAdvancing.Should().BeFalse();
    }

    [Fact]
    public void GetAuthoritativeSessionTimerSnapshot_WhenTreasureHuntSubstageActive_TracksMaximumTimeWindow()
    {
        var session = LiveSessionFactory.CreateScheduledMultiTargetTreasureHunt(maximumTimeMinutes: 5);
        var activeAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        Activate(session);

        var snapshot = session.GetAuthoritativeSessionTimerSnapshot(activeAt.AddMinutes(1));

        snapshot.TotalDuration.Should().Be(TimeSpan.FromMinutes(5));
        snapshot.RemainingDuration.Should().Be(TimeSpan.FromMinutes(4));
        snapshot.IsAdvancing.Should().BeTrue();
        snapshot.IsExpired.Should().BeFalse();
        snapshot.AdvancingSince.Should().Be(activeAt);
        session.IsMissionTimerAdvancing.Should().BeTrue();
    }

    [Fact]
    public void GetAuthoritativeSessionTimerSnapshot_WhenTreasureHuntPausedThenResumed_FreezesAndResumesSameWindow()
    {
        var session = LiveSessionFactory.CreateScheduledMultiTargetTreasureHunt(maximumTimeMinutes: 5);
        var activeAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        var pausedAt = activeAt.AddMinutes(1);
        var resumedAt = pausedAt.AddMinutes(2);
        Activate(session);

        session.MoveTo(SessionState.Paused, pausedAt, new SessionStateTransitionPolicy());
        var frozen = session.GetAuthoritativeSessionTimerSnapshot(pausedAt.AddMinutes(10));

        session.MoveTo(SessionState.Active, resumedAt, new SessionStateTransitionPolicy());
        var resumed = session.GetAuthoritativeSessionTimerSnapshot(resumedAt.AddMinutes(1));

        frozen.RemainingDuration.Should().Be(TimeSpan.FromMinutes(4));
        frozen.IsAdvancing.Should().BeFalse();
        frozen.AdvancingSince.Should().BeNull();
        resumed.RemainingDuration.Should().Be(TimeSpan.FromMinutes(3));
        resumed.IsAdvancing.Should().BeTrue();
        resumed.AdvancingSince.Should().Be(resumedAt);
        session.ActiveSubstageId.Should().NotBeNull();
    }

    [Fact]
    public void MarkMissionTimerExpiredIfElapsed_WhenTreasureHuntWindowElapsed_ExpiresWithoutAdvancing()
    {
        var session = LiveSessionFactory.CreateScheduledMultiTargetTreasureHunt(maximumTimeMinutes: 5);
        var activeAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        Activate(session);
        var activeSubstageId = session.ActiveSubstageId;
        session.ClearDomainEvents();

        var snapshot = session.MarkMissionTimerExpiredIfElapsed(activeAt.AddMinutes(5));

        snapshot.RemainingDuration.Should().Be(TimeSpan.Zero);
        snapshot.IsExpired.Should().BeTrue();
        snapshot.IsAdvancing.Should().BeFalse();
        snapshot.ExpiredAt.Should().Be(activeAt.AddMinutes(5));
        session.IsMissionTimerAdvancing.Should().BeFalse();
        session.ActiveSubstageId.Should().Be(activeSubstageId);
        session.State.Should().Be(SessionState.Active);
        session.DomainEvents.Should().BeEmpty();
    }

    // The mission deadline covers both play modes (D-4), so a trivia-only session is seeded too even
    // though nothing displays it as the primary window there.
    [Fact]
    public void GetMissionTimerSnapshot_WhenTriviaSessionStarts_TicksTheMissionDeadline()
    {
        var session = ActivateTriviaSession();
        var activeAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);

        var snapshot = session.GetMissionTimerSnapshot(activeAt.AddMinutes(1));

        session.HasMissionDeadline.Should().BeTrue();
        snapshot.TotalDuration.Should().Be(TimeSpan.FromMinutes(session.MaximumTime.Minutes));
        snapshot.RemainingDuration.Should().Be(TimeSpan.FromMinutes(session.MaximumTime.Minutes - 1));
        snapshot.IsAdvancing.Should().BeTrue();
        snapshot.AdvancingSince.Should().Be(activeAt);
    }

    // Paused time cannot burn the mission budget, and resuming must not restart it.
    [Fact]
    public void GetMissionTimerSnapshot_WhenTriviaSessionPausedThenResumed_FreezesTheMissionDeadline()
    {
        var session = ActivateTriviaSession();
        var activeAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        var pausedAt = activeAt.AddMinutes(1);
        var resumedAt = pausedAt.AddMinutes(10);
        var total = TimeSpan.FromMinutes(session.MaximumTime.Minutes);

        session.MoveTo(SessionState.Paused, pausedAt, new SessionStateTransitionPolicy());
        var frozen = session.GetMissionTimerSnapshot(pausedAt.AddMinutes(5));

        session.MoveTo(SessionState.Active, resumedAt, new SessionStateTransitionPolicy());
        var resumed = session.GetMissionTimerSnapshot(resumedAt.AddMinutes(1));

        // 10 minutes paused burn nothing: only the 1m before and the 1m after the pause count.
        frozen.RemainingDuration.Should().Be(total - TimeSpan.FromMinutes(1));
        frozen.IsAdvancing.Should().BeFalse();
        resumed.RemainingDuration.Should().Be(total - TimeSpan.FromMinutes(2));
        resumed.IsAdvancing.Should().BeTrue();
        resumed.TotalDuration.Should().Be(total);
    }

    [Fact]
    public void HasMissionDeadline_WhenSessionHasNotStarted_IsFalse()
    {
        var session = LiveSessionFactory.CreateScheduledTrivia();

        session.HasMissionDeadline.Should().BeFalse();
    }

    // Pause freezes the active-substage timer; Active resumes the SAME question at the frozen remainder.
    [Fact]
    public void GetAuthoritativeSessionTimerSnapshot_WhenPausedThenResumed_FreezesAndResumesSameQuestion()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        var pausedAt = activatedAt.AddSeconds(10);
        var resumedAt = pausedAt.AddSeconds(5);
        session.ActivateQuestion(0, activatedAt);

        session.MoveTo(SessionState.Paused, pausedAt, new SessionStateTransitionPolicy());
        var frozen = session.GetAuthoritativeSessionTimerSnapshot(pausedAt.AddSeconds(30));

        session.MoveTo(SessionState.Active, resumedAt, new SessionStateTransitionPolicy());
        var resumed = session.GetAuthoritativeSessionTimerSnapshot(resumedAt.AddSeconds(5));

        // frozen at the 20s remainder (30 - 10) regardless of observation time
        frozen.RemainingDuration.Should().Be(TimeSpan.FromSeconds(20));
        frozen.IsAdvancing.Should().BeFalse();
        frozen.AdvancingSince.Should().BeNull();

        // resumes the SAME question at the frozen remainder, then advances
        session.ActiveQuestionIndex.Should().Be(0);
        resumed.RemainingDuration.Should().Be(TimeSpan.FromSeconds(15));
        resumed.IsAdvancing.Should().BeTrue();
        resumed.AdvancingSince.Should().Be(resumedAt);
    }

    [Fact]
    public void GetAuthoritativeSessionTimerSnapshot_WhenObservedBeforeActivation_ReturnsFullQuestionWindow()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, activatedAt);

        var snapshot = session.GetAuthoritativeSessionTimerSnapshot(activatedAt.AddSeconds(-1));

        snapshot.RemainingDuration.Should().Be(TimeSpan.FromSeconds(30));
        snapshot.IsAdvancing.Should().BeTrue();
    }

    [Fact]
    public void MoveTo_ResumingExpiredQuestionTimer_KeepsOriginalExpiry()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        var expiredAt = activatedAt.AddSeconds(30);
        var pausedAt = expiredAt.AddSeconds(5);
        var resumedAt = pausedAt.AddSeconds(5);
        session.ActivateQuestion(0, activatedAt);
        session.MarkQuestionTimerExpiredIfElapsed(expiredAt);

        session.MoveTo(SessionState.Paused, pausedAt, new SessionStateTransitionPolicy());
        session.MoveTo(SessionState.Active, resumedAt, new SessionStateTransitionPolicy());

        var snapshot = session.GetAuthoritativeSessionTimerSnapshot(resumedAt);
        snapshot.RemainingDuration.Should().Be(TimeSpan.Zero);
        snapshot.IsExpired.Should().BeTrue();
        snapshot.ExpiredAt.Should().Be(expiredAt);
        snapshot.IsAdvancing.Should().BeFalse();
    }

    // AC #2 / OD-3: no whole-session `_sessionTimer*` countdown and no session-level `SessionMode` remain.
    [Fact]
    public void LiveSession_HasNoWholeSessionTimerOrSessionModeResidue()
    {
        var fieldNames = typeof(LiveSession)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(field => field.Name)
            .ToList();

        fieldNames.Should().NotContain(name => name.StartsWith("_sessionTimer", StringComparison.Ordinal));

        typeof(LiveSession).Assembly.GetTypes()
            .Select(type => type.Name)
            .Should().NotContain("SessionMode");
    }

    [Fact]
    public void MoveTo_ActiveWithOnlyNonAssociatedRuntimeTeam_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        session.RegisterTeam("Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();

        session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow, policy);

        var act = () => session.MoveTo(SessionState.Active, DateTimeOffset.UtcNow.AddMinutes(1), policy);

        act.Should().Throw<LiveSessionRequiresAtLeastOneTeamException>();
    }

    [Fact]
    public void ActivateQuestion_WhenSessionIsActive_ActivatesQuestionAndStartsTimer()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 5, TimeSpan.Zero);

        session.ActivateQuestion(0, activatedAt);

        session.ActiveQuestionIndex.Should().Be(0);
        session.IsQuestionTimerAdvancing.Should().BeTrue();

        var snapshot = session.GetActiveQuestionTimerSnapshot(activatedAt.AddSeconds(10));
        snapshot.TotalDuration.Should().Be(TimeSpan.FromSeconds(30));
        snapshot.RemainingDuration.Should().Be(TimeSpan.FromSeconds(20));
        snapshot.IsAdvancing.Should().BeTrue();
        snapshot.AdvancingSince.Should().Be(activatedAt);

        var activatedEvent = session.DomainEvents.OfType<QuestionActivatedEvent>().Single();
        activatedEvent.LiveSessionId.Should().Be(session.LiveSessionId);
        activatedEvent.QuestionIndex.Should().Be(0);
        activatedEvent.SequenceOrder.Should().Be(1);
        activatedEvent.TimeLimitSeconds.Should().Be(30);
        activatedEvent.ActivatedAt.Should().Be(activatedAt);
    }

    [Fact]
    public void ActivateQuestion_WhenSessionIsNotActive_ThrowsException()
    {
        var session = LiveSessionFactory.CreateScheduledTrivia();

        var act = () => session.ActivateQuestion(0, DateTimeOffset.UtcNow);

        act.Should().Throw<QuestionActivationRequiresActiveSessionException>();
    }

    [Fact]
    public void ActivateQuestion_WhenIndexIsOutOfBounds_ThrowsException()
    {
        var session = ActivateTriviaSession();

        var act = () => session.ActivateQuestion(1, DateTimeOffset.UtcNow);

        act.Should().Throw<QuestionIndexOutOfRangeException>();
    }

    [Fact]
    public void ActivateQuestion_WhenQuestionIsAlreadyActive_ThrowsException()
    {
        var session = ActivateTriviaSession();
        session.ActivateQuestion(0, DateTimeOffset.UtcNow);

        var act = () => session.ActivateQuestion(0, DateTimeOffset.UtcNow.AddSeconds(1));

        act.Should().Throw<QuestionAlreadyActiveException>();
    }

    [Fact]
    public void MarkQuestionTimerExpiredIfElapsed_BeforeTimeRunsOut_ReturnsNotExpiredSnapshot()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, activatedAt);

        var snapshot = session.MarkQuestionTimerExpiredIfElapsed(activatedAt.AddSeconds(20));

        snapshot.RemainingDuration.Should().Be(TimeSpan.FromSeconds(10));
        snapshot.IsExpired.Should().BeFalse();
        snapshot.IsAdvancing.Should().BeTrue();
        snapshot.ExpiredAt.Should().BeNull();
        session.IsQuestionTimerAdvancing.Should().BeTrue();
    }

    [Fact]
    public void MarkQuestionTimerExpiredIfElapsed_WhenElapsed_FreezesAtZero()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        var expiredAt = activatedAt.AddSeconds(30);
        session.ActivateQuestion(0, activatedAt);

        var snapshot = session.MarkQuestionTimerExpiredIfElapsed(expiredAt);

        snapshot.RemainingDuration.Should().Be(TimeSpan.Zero);
        snapshot.IsExpired.Should().BeTrue();
        snapshot.IsAdvancing.Should().BeFalse();
        snapshot.ExpiredAt.Should().Be(expiredAt);
        session.IsQuestionTimerAdvancing.Should().BeFalse();
    }

    [Fact]
    public void CloseActiveQuestion_WhenQuestionIsActive_ClearsActiveQuestionAndRaisesEvent()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        var closedAt = activatedAt.AddSeconds(12);
        session.ActivateQuestion(0, activatedAt);

        session.CloseActiveQuestion(closedAt);

        session.ActiveQuestionIndex.Should().BeNull();
        session.IsQuestionTimerAdvancing.Should().BeFalse();

        var closedEvent = session.DomainEvents.OfType<QuestionClosedEvent>().Single();
        closedEvent.LiveSessionId.Should().Be(session.LiveSessionId);
        closedEvent.QuestionIndex.Should().Be(0);
        closedEvent.ClosedAt.Should().Be(closedAt);
        closedEvent.WasExpiredByTimer.Should().BeFalse();
    }

    [Fact]
    public void CloseActiveQuestion_WhenQuestionExpired_ReportsTimerExpiry()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        var expiredAt = activatedAt.AddSeconds(30);
        session.ActivateQuestion(0, activatedAt);
        session.MarkQuestionTimerExpiredIfElapsed(expiredAt);

        session.CloseActiveQuestion(expiredAt);

        session.DomainEvents
            .OfType<QuestionClosedEvent>()
            .Single()
            .WasExpiredByTimer
            .Should()
            .BeTrue();
    }

    [Fact]
    public void CloseActiveQuestion_WhenNoQuestionIsActive_ThrowsException()
    {
        var session = ActivateTriviaSession();

        var act = () => session.CloseActiveQuestion(DateTimeOffset.UtcNow);

        act.Should().Throw<NoActiveQuestionException>();
    }

    [Fact]
    public void CloseActiveQuestionForReveal_ClosesQuestionAndOpensRevealWindow()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        var closedAt = activatedAt.AddSeconds(30);
        session.ActivateQuestion(0, activatedAt);

        session.CloseActiveQuestionForReveal(closedAt, TimeSpan.FromSeconds(5), nextQuestionIndex: 1);

        session.ActiveQuestionIndex.Should().BeNull();
        session.IsAwaitingQuestionReveal.Should().BeTrue();
        session.QuestionRevealUntil.Should().Be(closedAt.AddSeconds(5));
        session.IsQuestionRevealElapsed(closedAt.AddSeconds(4)).Should().BeFalse();
        session.IsQuestionRevealElapsed(closedAt.AddSeconds(5)).Should().BeTrue();
        session.DomainEvents.OfType<QuestionClosedEvent>().Single().QuestionIndex.Should().Be(0);
    }

    [Fact]
    public void CompleteQuestionRevealAndDequeueNext_ReturnsPendingIndexAndClearsRevealWindow()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, activatedAt);
        session.CloseActiveQuestionForReveal(activatedAt.AddSeconds(30), TimeSpan.FromSeconds(5), nextQuestionIndex: 1);

        var next = session.CompleteQuestionRevealAndDequeueNext();

        next.Should().Be(1);
        session.IsAwaitingQuestionReveal.Should().BeFalse();
        session.QuestionRevealUntil.Should().BeNull();
    }

    [Fact]
    public void CompleteQuestionRevealAndDequeueNext_WhenSubstageExhausted_ReturnsNull()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, activatedAt);
        session.CloseActiveQuestionForReveal(activatedAt.AddSeconds(30), TimeSpan.FromSeconds(5), nextQuestionIndex: null);

        session.CompleteQuestionRevealAndDequeueNext().Should().BeNull();
        session.IsAwaitingQuestionReveal.Should().BeFalse();
    }

    [Fact]
    public void CompleteQuestionRevealAndDequeueNext_WhenNoRevealPending_ThrowsException()
    {
        var session = ActivateTriviaSession();

        var act = () => session.CompleteQuestionRevealAndDequeueNext();

        act.Should().Throw<NoActiveQuestionRevealException>();
    }

    [Fact]
    public void IsQuestionTimerAdvancing_IsTrueOnlyWhenActiveStateOwnsRunningQuestionTimer()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        var pausedAt = activatedAt.AddSeconds(10);
        session.ActivateQuestion(0, activatedAt);

        session.IsQuestionTimerAdvancing.Should().BeTrue();

        session.MoveTo(SessionState.Paused, pausedAt, new SessionStateTransitionPolicy());

        session.IsQuestionTimerAdvancing.Should().BeFalse();
    }

    [Fact]
    public void MoveTo_PausedFreezesQuestionTimerAndResumeRestoresAdvancing()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        var pausedAt = activatedAt.AddSeconds(10);
        var resumedAt = pausedAt.AddSeconds(5);
        session.ActivateQuestion(0, activatedAt);

        session.MoveTo(SessionState.Paused, pausedAt, new SessionStateTransitionPolicy());
        var pausedSnapshot = session.GetActiveQuestionTimerSnapshot(pausedAt.AddSeconds(10));

        session.MoveTo(SessionState.Active, resumedAt, new SessionStateTransitionPolicy());
        var resumedSnapshot = session.GetActiveQuestionTimerSnapshot(resumedAt.AddSeconds(5));

        pausedSnapshot.RemainingDuration.Should().Be(TimeSpan.FromSeconds(20));
        pausedSnapshot.IsAdvancing.Should().BeFalse();
        resumedSnapshot.RemainingDuration.Should().Be(TimeSpan.FromSeconds(15));
        resumedSnapshot.IsAdvancing.Should().BeTrue();
        resumedSnapshot.AdvancingSince.Should().Be(resumedAt);
    }

    [Fact]
    public void SequentialQuestionActivationStrategy_WhenNoQuestionIsActive_ReturnsFirstBySequence()
    {
        // The strategy is substage-scoped now: it needs an active substage (an Active session) to
        // count questions. On a fresh active substage with no question yet it returns 0.
        var session = ActivateTriviaSessionWithThreeQuestions();
        var strategy = new SequentialQuestionActivationStrategy();

        var next = strategy.Next(session);

        next.Should().Be(0);
    }

    [Fact]
    public void SequentialQuestionActivationStrategy_WhenSecondTriviaSubstageIsActive_CountsOnlyThatSubstage()
    {
        var session = LiveSessionFactory.CreateScheduledMultiSubstageTrivia();
        Activate(session);
        var advancedAt = new DateTimeOffset(2026, 6, 3, 10, 2, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, advancedAt);
        session.CloseActiveQuestion(advancedAt.AddSeconds(30));
        session.CompleteActiveSubstageAndAdvance(advancedAt.AddSeconds(30), new SessionStateTransitionPolicy());
        var strategy = new SequentialQuestionActivationStrategy();

        var next = strategy.Next(session);

        next.Should().Be(0);
    }

    [Fact]
    public void MoveTo_EnteringActive_SetsActiveSubstageToFirstSubstageInStrictOrder()
    {
        var session = LiveSessionFactory.CreateScheduledMultiSubstageTrivia();
        Activate(session);

        session.ActiveSubstageId.Should().Be(OrderedSubstages(session)[0].SubstageSnapshotId);
    }

    [Fact]
    public void MoveTo_PausedThenResumed_KeepsActiveSubstagePointer()
    {
        var session = LiveSessionFactory.CreateScheduledMultiSubstageTrivia();
        Activate(session);
        var firstSubstageId = session.ActiveSubstageId;
        var pausedAt = new DateTimeOffset(2026, 6, 3, 10, 5, 0, TimeSpan.Zero);

        session.MoveTo(SessionState.Paused, pausedAt, new SessionStateTransitionPolicy());
        session.MoveTo(SessionState.Active, pausedAt.AddMinutes(1), new SessionStateTransitionPolicy());

        session.ActiveSubstageId.Should().Be(firstSubstageId);
    }

    [Fact]
    public void ActivateQuestion_ScopesQuestionRangeToActiveSubstageOnly()
    {
        // Two trivia substages, one question each. The active (first) substage exposes exactly one
        // question, so index 1 (which would exist in the flat snapshot) is out of range.
        var session = LiveSessionFactory.CreateScheduledMultiSubstageTrivia();
        Activate(session);

        var act = () => session.ActivateQuestion(1, DateTimeOffset.UtcNow);

        act.Should().Throw<QuestionIndexOutOfRangeException>();
    }

    [Fact]
    public void CompleteActiveSubstageAndAdvance_WhenNextSubstageIsTrivia_MovesPointerAndReadiesFirstQuestion()
    {
        var session = LiveSessionFactory.CreateScheduledMultiSubstageTrivia();
        Activate(session);
        var substages = OrderedSubstages(session);
        var advancedAt = new DateTimeOffset(2026, 6, 3, 10, 2, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, advancedAt);
        session.CloseActiveQuestion(advancedAt.AddSeconds(30));

        session.CompleteActiveSubstageAndAdvance(advancedAt.AddSeconds(30), new SessionStateTransitionPolicy());

        session.State.Should().Be(SessionState.Active);
        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);
        session.ActiveQuestionIndex.Should().BeNull();

        var advancedEvent = session.DomainEvents.OfType<SubstageAdvancedEvent>().Single();
        advancedEvent.LiveSessionId.Should().Be(session.LiveSessionId);
        advancedEvent.FromSubstageId.Should().Be(substages[0].SubstageSnapshotId);
        advancedEvent.FromPlayMode.Should().Be(SubstagePlayMode.Trivia);
        advancedEvent.ToSubstageId.Should().Be(substages[1].SubstageSnapshotId);

        // The next substage's first question is ready to activate (strategy returns position 0).
        new SequentialQuestionActivationStrategy().Next(session).Should().Be(0);
    }

    // Advancing into a treasure hunt must not restart the clock: MaximumTime is one budget for the
    // whole mission (D-4), seeded at start, so the hunt inherits whatever is left of it.
    [Fact]
    public void CompleteActiveSubstageAndAdvance_WhenNextSubstageIsTreasureHunt_KeepsTheRunningMissionDeadline()
    {
        var session = LiveSessionFactory.CreateScheduledTriviaThenTreasureHunt();
        Activate(session);
        var activeAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        var substages = OrderedSubstages(session);
        var advancedAt = new DateTimeOffset(2026, 6, 3, 10, 2, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, advancedAt);
        session.CloseActiveQuestion(advancedAt.AddSeconds(30));

        session.CompleteActiveSubstageAndAdvance(advancedAt.AddSeconds(30), new SessionStateTransitionPolicy());

        session.State.Should().Be(SessionState.Active);
        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);
        session.ActiveQuestionIndex.Should().BeNull();
        substages[1].PlayMode.Should().Be(SubstagePlayMode.TreasureHunt);

        var advancedEvent = session.DomainEvents.OfType<SubstageAdvancedEvent>().Single();
        advancedEvent.FromPlayMode.Should().Be(SubstagePlayMode.Trivia);
        advancedEvent.ToSubstageId.Should().Be(substages[1].SubstageSnapshotId);

        // Observed 2m30s after the session started, so the deadline reads 42m30s left — not the full
        // 45m a per-substage reseed would have shown.
        var timer = session.GetAuthoritativeSessionTimerSnapshot(advancedAt.AddSeconds(30).AddMinutes(1));
        timer.TotalDuration.Should().Be(TimeSpan.FromMinutes(45));
        timer.RemainingDuration.Should().Be(TimeSpan.FromMinutes(42.5));
        timer.IsAdvancing.Should().BeTrue();
        timer.AdvancingSince.Should().Be(activeAt);
    }

    [Fact]
    public void CompleteActiveSubstageAndAdvance_WhenNoNextSubstage_FinishesViaSessionCompletion()
    {
        var session = ActivateTriviaSession();
        var substages = OrderedSubstages(session);
        var advancedAt = new DateTimeOffset(2026, 6, 3, 10, 2, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, advancedAt);
        session.CloseActiveQuestion(advancedAt.AddSeconds(30));

        session.CompleteActiveSubstageAndAdvance(advancedAt.AddSeconds(30), new SessionStateTransitionPolicy());

        // Finished is reached ONLY here (SessionCompletion) — never on flat-list exhaustion.
        session.State.Should().Be(SessionState.Finished);
        session.EndedAt.Should().Be(advancedAt.AddSeconds(30));

        var advancedEvent = session.DomainEvents.OfType<SubstageAdvancedEvent>().Single();
        advancedEvent.FromSubstageId.Should().Be(substages[0].SubstageSnapshotId);
        advancedEvent.FromPlayMode.Should().Be(SubstagePlayMode.Trivia);
        advancedEvent.ToSubstageId.Should().BeNull();

        // HU-33B: the →Finished fact (raised ONLY here, via SessionCompletion) carries the
        // history-correlation fields the async publication/history consumer needs.
        var finishedEvent = session.DomainEvents.OfType<SessionStateChangedEvent>()
            .Single(stateEvent => stateEvent.CurrentState == SessionState.Finished);
        finishedEvent.LiveSessionId.Should().Be(session.LiveSessionId);
        finishedEvent.PreviousState.Should().Be(SessionState.Active);
        finishedEvent.ChangedAt.Should().Be(advancedAt.AddSeconds(30));
        finishedEvent.ResponsibleUserId.Should().BeNull();
        finishedEvent.Reason.Should().BeNull();
        finishedEvent.ActorType.Should().Be(SessionEventActorType.System);

        var auditRecord = session.SessionEvents
            .Single(sessionEvent => sessionEvent.PayloadSummary == "Active→Finished");
        auditRecord.OccurredAt.Should().Be(advancedAt.AddSeconds(30));
        auditRecord.ActorType.Should().Be(SessionEventActorType.System);
        auditRecord.ActorId.Should().BeNull();
    }

    [Fact]
    public void CompleteActiveSubstageAndAdvance_WhenPaused_IsFrozenAndRejected()
    {
        var session = ActivateTriviaSession();
        session.MoveTo(SessionState.Paused, new DateTimeOffset(2026, 6, 3, 10, 5, 0, TimeSpan.Zero), new SessionStateTransitionPolicy());

        var act = () => session.CompleteActiveSubstageAndAdvance(DateTimeOffset.UtcNow, new SessionStateTransitionPolicy());

        act.Should().Throw<SubstageAdvancementRequiresActiveSessionException>();
        session.DomainEvents.OfType<SubstageAdvancedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void CompleteActiveSubstageAndAdvance_WhenActivePointerIsMissing_ThrowsNoActiveSubstage()
    {
        var session = ActivateTriviaSession();
        SetPrivateProperty(session, nameof(LiveSession.ActiveSubstageId), null);

        var act = () => session.CompleteActiveSubstageAndAdvance(DateTimeOffset.UtcNow, new SessionStateTransitionPolicy());

        act.Should().Throw<NoActiveSubstageException>();
    }

    [Fact]
    public void SequentialQuestionActivationStrategy_WhenQuestionIsActive_ReturnsNextBySequence()
    {
        var session = ActivateTriviaSessionWithThreeQuestions();
        var strategy = new SequentialQuestionActivationStrategy();
        session.ActivateQuestion(0, DateTimeOffset.UtcNow);

        var next = strategy.Next(session);

        next.Should().Be(1);
    }

    [Fact]
    public void SequentialQuestionActivationStrategy_WhenLastQuestionIsActive_ReturnsNull()
    {
        var session = ActivateTriviaSessionWithThreeQuestions();
        var strategy = new SequentialQuestionActivationStrategy();
        session.ActivateQuestion(2, DateTimeOffset.UtcNow);

        var next = strategy.Next(session);

        next.Should().BeNull();
    }

    [Fact]
    public void SequentialQuestionActivationStrategy_WhenPersistedQuestionIndexIsNegative_ReturnsNull()
    {
        var session = ActivateTriviaSessionWithThreeQuestions();
        SetPrivateProperty(session, nameof(LiveSession.ActiveQuestionIndex), -1);
        var strategy = new SequentialQuestionActivationStrategy();

        var next = strategy.Next(session);

        next.Should().BeNull();
    }

    // HU-33B D-1: session-ops EMITS the round-close/finish facts but computes no puntaje/ranking
    // (that is ScoringMonitoring, derived downstream). Driving a full trivia round to Finished leaves
    // every team's score untouched — the domain never scores on close/finish.
    [Fact]
    public void TriviaRound_OnCloseAndFinishViaSessionCompletion_ComputesNoTeamScoreOrRanking()
    {
        var session = ActivateTriviaSession();
        var at = new DateTimeOffset(2026, 6, 3, 10, 2, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, at);
        session.CloseActiveQuestion(at.AddSeconds(30));
        session.CompleteActiveSubstageAndAdvance(at.AddSeconds(30), new SessionStateTransitionPolicy());

        session.State.Should().Be(SessionState.Finished);
        session.Teams.Should().NotBeEmpty()
            .And.OnlyContain(team => team.CurrentScore == null && team.LastScoreCalculatedAt == null);
    }

    // HU-33B D-2: the publishable facts are the REUSED QuestionClosedEvent + SessionStateChangedEvent —
    // no new Domain/Events/* type is introduced for round-close/finish publication (SessionResultsFinalized
    // is an Application integration-event contract, not a domain event, and scoring/ranking stay downstream).
    [Fact]
    public void Domain_HasNoNewPublicationEvent_ReusesQuestionClosedAndSessionStateChangedFacts()
    {
        var domainEventTypeNames = typeof(SessionStateChangedEvent).Assembly.GetTypes()
            .Where(type => typeof(BaseEvent).IsAssignableFrom(type) && !type.IsAbstract)
            .Select(type => type.Name)
            .ToList();

        domainEventTypeNames.Should().Contain(new[] { nameof(QuestionClosedEvent), nameof(SessionStateChangedEvent) });
        domainEventTypeNames.Should().NotContain(name =>
            name.Contains("Score", StringComparison.Ordinal) ||
            name.Contains("Ranking", StringComparison.Ordinal) ||
            name.Contains("Finalized", StringComparison.Ordinal));
    }

    // ── HU-31: target-scan registration and retained rejection ───────────────────────────────────

    [Fact]
    public void RegisterTargetScan_WhenQrMatchesActiveTarget_AcceptsAndRaisesOrderedFactsWithRelayedScore()
    {
        var session = LiveSessionFactory.CreateScheduledMultiTargetTreasureHunt();
        Activate(session);
        var team = session.Teams.Single();
        var participantId = Guid.NewGuid();
        var submittedAt = new DateTimeOffset(2026, 7, 13, 10, 1, 5, TimeSpan.Zero);
        var target = session.MissionRuntimeSnapshot.TargetSnapshots.Single(target => target.Score == 200);

        var submission = session.RegisterTargetScan(team.TeamId, "QR-003", participantId, submittedAt);

        session.TreasureEvidenceSubmissions.Should().ContainSingle().Which.Should().Be(submission);
        submission.ValidationState.Should().Be(EvidenceValidationState.Accepted);
        submission.TargetSnapshotId.Should().Be(target.TargetSnapshotId);
        submission.RejectionReason.Should().BeNull();

        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().ContainSingle();
        var registeredEvent = session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Single();
        registeredEvent.OriginReference.Should().Be($"target:{target.TargetSnapshotId}");
        var registeredIndex = session.DomainEvents.ToList().FindIndex(domainEvent =>
            domainEvent is EvidenceSubmissionRegisteredEvent);
        var resolvedIndex = session.DomainEvents.ToList().FindIndex(domainEvent =>
            domainEvent is TargetResolvedEvent);
        registeredIndex.Should().BeLessThan(resolvedIndex);

        var resolved = session.DomainEvents.OfType<TargetResolvedEvent>().Single();
        resolved.LiveSessionId.Should().Be(session.LiveSessionId);
        resolved.TeamId.Should().Be(team.TeamId);
        resolved.EvidenceSubmissionId.Should().Be(submission.EvidenceSubmissionId);
        resolved.ActiveSubstageId.Should().Be(session.ActiveSubstageId!.Value);
        resolved.TargetSnapshotId.Should().Be(target.TargetSnapshotId);
        resolved.ScoreValue.Should().Be(target.Score);
        resolved.ResolvedAt.Should().Be(submittedAt);

        submission.DomainEvents.OfType<EvidenceSubmissionAcceptedEvent>().Should().ContainSingle()
            .Which.EvidenceSubmissionId.Should().Be(submission.EvidenceSubmissionId);

        session.ProjectParticipantTeamBoard(team.TeamId, submittedAt)
            .ActiveSubstageContext!.ResolvedTargets.Should().Be(1);
    }

    [Fact]
    public void RegisterTargetScan_WhenQrDoesNotResolve_RetainsRejectedEvidenceAndRaisesOnlyRegistrationFact()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        Activate(session);
        var team = session.Teams.Single();

        var submission = session.RegisterTargetScan(
            team.TeamId,
            "WRONG-QR",
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 13, 10, 1, 5, TimeSpan.Zero));

        submission.ValidationState.Should().Be(EvidenceValidationState.Rejected);
        submission.TargetSnapshotId.Should().BeNull();
        submission.ResolutionRejectionReason.Should()
            .Be(TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget);
        session.TreasureEvidenceSubmissions.Should().ContainSingle().Which.Should().Be(submission);
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().ContainSingle();
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Single()
            .OriginReference.Should().Be("qr:WRONG-QR");
        session.DomainEvents.OfType<TargetResolvedEvent>().Should().BeEmpty();
        submission.DomainEvents.OfType<EvidenceSubmissionRejectedEvent>().Should().ContainSingle()
            .Which.RejectionReason.Should().Be("The scanned value does not resolve to a target.");
    }

    [Fact]
    public void RegisterTargetScan_WhenTargetAlreadyResolvedByTeam_RetainsDuplicateAsRejected()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        Activate(session);
        var team = session.Teams.Single();
        var submittedAt = new DateTimeOffset(2026, 7, 13, 10, 1, 5, TimeSpan.Zero);
        session.RegisterTargetScan(team.TeamId, "QR-001", Guid.NewGuid(), submittedAt);

        var duplicate = session.RegisterTargetScan(
            team.TeamId,
            "QR-001",
            Guid.NewGuid(),
            submittedAt.AddSeconds(1));

        duplicate.ValidationState.Should().Be(EvidenceValidationState.Rejected);
        duplicate.ResolutionRejectionReason.Should()
            .Be(TargetResolutionRejectionReason.TargetAlreadyResolvedByTeam);
        session.TreasureEvidenceSubmissions.Should().HaveCount(2);
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().HaveCount(2);
        session.DomainEvents.OfType<TargetResolvedEvent>().Should().ContainSingle();
        duplicate.DomainEvents.OfType<EvidenceSubmissionRejectedEvent>().Should().ContainSingle()
            .Which.RejectionReason.Should().Be("The target has already been resolved by this team.");
        session.ProjectParticipantTeamBoard(team.TeamId, submittedAt.AddSeconds(1))
            .ActiveSubstageContext!.ResolvedTargets.Should().Be(1);
    }

    [Fact]
    public void RegisterTargetScan_WhenQrMatchesTargetOutsideActiveSubstage_RetainsRejectedEvidence()
    {
        var session = CreateTwoTreasureSubstageSessionWithTargetInSecondSubstage();
        Activate(session);
        var team = session.Teams.Single();

        var submission = session.RegisterTargetScan(
            team.TeamId,
            "QR-LATER",
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 13, 10, 1, 5, TimeSpan.Zero));

        submission.ValidationState.Should().Be(EvidenceValidationState.Rejected);
        submission.ResolutionRejectionReason.Should()
            .Be(TargetResolutionRejectionReason.TargetOutsideActiveSubstage);
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().ContainSingle();
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Single()
            .OriginReference.Should().Be($"target:{submission.TargetSnapshotId}");
        session.DomainEvents.OfType<TargetResolvedEvent>().Should().BeEmpty();
        submission.DomainEvents.OfType<EvidenceSubmissionRejectedEvent>().Should().ContainSingle()
            .Which.RejectionReason.Should().Be("The resolved target does not belong to the active treasure-hunt substage.");
    }

    [Theory]
    [InlineData(SessionState.Paused)]
    [InlineData(SessionState.Cancelled)]
    public void RegisterTargetScan_WhenSessionDoesNotAdmitEvidence_RejectsBeforeRegistration(SessionState state)
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        Activate(session);
        var team = session.Teams.Single();
        session.MoveTo(
            state,
            new DateTimeOffset(2026, 7, 13, 10, 2, 0, TimeSpan.Zero),
            new SessionStateTransitionPolicy());

        var act = () => session.RegisterTargetScan(
            team.TeamId,
            "QR-001",
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 13, 10, 2, 1, TimeSpan.Zero));

        act.Should().Throw<TriviaAnswerRequiresActiveSessionException>();
        session.TreasureEvidenceSubmissions.Should().BeEmpty();
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().BeEmpty();
        session.DomainEvents.OfType<TargetResolvedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void RegisterTargetScan_WhenSessionFinished_RejectsBeforeRegistration()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        Activate(session);
        var team = session.Teams.Single();
        var finishedAt = new DateTimeOffset(2026, 7, 13, 10, 2, 0, TimeSpan.Zero);
        session.CompleteActiveSubstageAndAdvance(finishedAt, new SessionStateTransitionPolicy());

        var act = () => session.RegisterTargetScan(
            team.TeamId,
            "QR-001",
            Guid.NewGuid(),
            finishedAt.AddSeconds(1));

        act.Should().Throw<TriviaAnswerRequiresActiveSessionException>();
        session.TreasureEvidenceSubmissions.Should().BeEmpty();
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().BeEmpty();
        session.DomainEvents.OfType<TargetResolvedEvent>().Should().BeEmpty();
    }

    // ── HU-34: answer-registration Template Method skeleton ───────────────────────────────────────
    // One workflow governs accept + reject. These tests lock every branch of the single write.

    [Fact]
    public void RegisterTriviaAnswer_WhenFirstInTimeCorrectAnswer_AcceptsOnceAndSnapshotsCorrectnessAndScore()
    {
        var session = ActivateTriviaSession();
        var team = session.Teams.First();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        var submittedAt = activatedAt.AddSeconds(5);
        var participantId = Guid.NewGuid();
        session.ActivateQuestion(0, activatedAt);

        var submission = session.RegisterTriviaAnswer(team.TeamId, selectedOptionSequenceOrder: 1, participantId, submittedAt);

        session.TriviaAnswerSubmissions.Should().ContainSingle().Which.Should().Be(submission);
        submission.TeamId.Should().Be(team.TeamId);
        submission.LiveSessionId.Should().Be(session.LiveSessionId);
        submission.ActiveSubstageId.Should().Be(session.ActiveSubstageId!.Value);
        submission.QuestionSequenceOrder.Should().Be(1);
        submission.SelectedOptionSequenceOrder.Should().Be(1);
        submission.IsCorrect.Should().BeTrue();
        submission.ScoreValue.Should().Be(100);
        submission.SubmissionType.Should().Be(EvidenceSubmissionType.TriviaAnswer);
        submission.ValidationState.Should().Be(EvidenceValidationState.Accepted);
        submission.SubmittedByParticipantId.Should().Be(participantId);
        submission.SubmittedAt.Should().Be(submittedAt);

        var registered = session.DomainEvents.OfType<AnswerRegisteredEvent>().Single();
        registered.LiveSessionId.Should().Be(session.LiveSessionId);
        registered.TeamId.Should().Be(team.TeamId);
        registered.EvidenceSubmissionId.Should().Be(submission.EvidenceSubmissionId);
        registered.ActiveSubstageId.Should().Be(session.ActiveSubstageId!.Value);
        registered.QuestionSequenceOrder.Should().Be(1);
        registered.SelectedOptionSequenceOrder.Should().Be(1);
        registered.IsCorrect.Should().BeTrue();
        registered.ScoreValue.Should().Be(100);
        registered.SubmittedAt.Should().Be(submittedAt);

        var evidenceRegistered = session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Single();
        evidenceRegistered.LiveSessionId.Should().Be(session.LiveSessionId);
        evidenceRegistered.TeamId.Should().Be(team.TeamId);
        evidenceRegistered.EvidenceSubmissionId.Should().Be(submission.EvidenceSubmissionId);
        evidenceRegistered.ActiveSubstageId.Should().Be(session.ActiveSubstageId.Value);
        evidenceRegistered.SubmissionType.Should().Be(EvidenceSubmissionType.TriviaAnswer);
        evidenceRegistered.SubmittedAt.Should().Be(submittedAt);
        evidenceRegistered.ValidationState.Should().Be(EvidenceValidationState.Pending);
        evidenceRegistered.OriginReference.Should().Be("question:1");
        submission.DomainEvents.OfType<EvidenceSubmissionAcceptedEvent>().Should().ContainSingle()
            .Which.EvidenceSubmissionId.Should().Be(submission.EvidenceSubmissionId);
    }

    [Fact]
    public void RegisterTriviaAnswer_WhenTeamIsAddressedByReferenceId_AcceptsForRuntimeTeam()
    {
        var session = ActivateTriviaSession();
        var team = session.Teams.First();
        var referenceTeamId = team.ReferenceTeamId!.Value;
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, activatedAt);

        var submission = session.RegisterTriviaAnswer(
            referenceTeamId,
            selectedOptionSequenceOrder: 1,
            Guid.NewGuid(),
            activatedAt.AddSeconds(5));

        submission.TeamId.Should().Be(team.TeamId);
    }

    [Fact]
    public void RegisterTriviaAnswer_WhenDifferentTeamAlreadyAnswered_AllowsFirstAnswerForSecondTeam()
    {
        var session = LiveSessionFactory.CreateScheduledTrivia();
        var firstTeam = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var secondTeam = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);
        var policy = new SessionStateTransitionPolicy();
        var activeAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, activeAt, policy);
        session.ActivateQuestion(0, activeAt);
        session.RegisterTriviaAnswer(firstTeam.TeamId, selectedOptionSequenceOrder: 1, Guid.NewGuid(), activeAt.AddSeconds(3));

        var submission = session.RegisterTriviaAnswer(
            secondTeam.TeamId,
            selectedOptionSequenceOrder: 1,
            Guid.NewGuid(),
            activeAt.AddSeconds(5));

        submission.TeamId.Should().Be(secondTeam.TeamId);
        session.TriviaAnswerSubmissions.Should().HaveCount(2);
    }

    [Fact]
    public void RegisterTriviaAnswer_WhenWrongOptionSelected_AcceptsAsTeamAnswerWithZeroScore()
    {
        var session = ActivateTriviaSession();
        var team = session.Teams.First();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, activatedAt);

        var submission = session.RegisterTriviaAnswer(team.TeamId, selectedOptionSequenceOrder: 2, submittedByParticipantId: Guid.NewGuid(), activatedAt.AddSeconds(5));

        submission.ValidationState.Should().Be(EvidenceValidationState.Accepted);
        submission.IsCorrect.Should().BeFalse();
        submission.ScoreValue.Should().Be(0);
        session.DomainEvents.OfType<AnswerRegisteredEvent>().Single().IsCorrect.Should().BeFalse();
    }

    [Fact]
    public void RegisterTriviaAnswer_WhenTeamAlreadyAnswered_RejectsDuplicateAndKeepsFirstWrite()
    {
        var session = ActivateTriviaSession();
        var team = session.Teams.First();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, activatedAt);
        session.RegisterTriviaAnswer(team.TeamId, selectedOptionSequenceOrder: 1, Guid.NewGuid(), activatedAt.AddSeconds(3));

        var act = () => session.RegisterTriviaAnswer(team.TeamId, selectedOptionSequenceOrder: 2, Guid.NewGuid(), activatedAt.AddSeconds(6));

        act.Should().Throw<DuplicateTriviaAnswerException>();
        session.TriviaAnswerSubmissions.Should().ContainSingle();
        session.TriviaAnswerSubmissions.Single().SelectedOptionSequenceOrder.Should().Be(1); // first write wins
        session.DomainEvents.OfType<AnswerRegisteredEvent>().Should().ContainSingle();
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().ContainSingle();
    }

    [Fact]
    public void RegisterTriviaAnswer_WhenTimerWindowClosed_RejectsLateAndRaisesNoEvent()
    {
        var session = ActivateTriviaSession();
        var team = session.Teams.First();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, activatedAt);

        var act = () => session.RegisterTriviaAnswer(team.TeamId, selectedOptionSequenceOrder: 1, Guid.NewGuid(), activatedAt.AddSeconds(31));

        act.Should().Throw<LateTriviaAnswerException>();
        session.TriviaAnswerSubmissions.Should().BeEmpty();
        session.DomainEvents.OfType<AnswerRegisteredEvent>().Should().BeEmpty();
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().BeEmpty();
    }

    [Fact]
    public void RegisterTriviaAnswer_WhenNoQuestionActive_RejectsAndRaisesNoEvent()
    {
        var session = ActivateTriviaSession();
        var team = session.Teams.First();

        var act = () => session.RegisterTriviaAnswer(team.TeamId, selectedOptionSequenceOrder: 1, Guid.NewGuid(), new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero));

        act.Should().Throw<TriviaAnswerRequiresActiveQuestionException>();
        session.DomainEvents.OfType<AnswerRegisteredEvent>().Should().BeEmpty();
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().BeEmpty();
    }

    [Fact]
    public void RegisterTriviaAnswer_WhenTeamIsUnknown_RejectsWithoutSubmissionOrRegistrationEvent()
    {
        var session = ActivateTriviaSession();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, activatedAt);

        var act = () => session.RegisterTriviaAnswer(
            Guid.NewGuid(),
            selectedOptionSequenceOrder: 1,
            Guid.NewGuid(),
            activatedAt.AddSeconds(5));

        act.Should().Throw<TeamNotFoundException>();
        session.TriviaAnswerSubmissions.Should().BeEmpty();
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().BeEmpty();
    }

    [Fact]
    public void RegisterTriviaAnswer_WhenSelectedOptionIsNotInSnapshot_Rejects()
    {
        var session = ActivateTriviaSession();
        var team = session.Teams.First();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, activatedAt);

        var act = () => session.RegisterTriviaAnswer(team.TeamId, selectedOptionSequenceOrder: 99, Guid.NewGuid(), activatedAt.AddSeconds(5));

        act.Should().Throw<InvalidTriviaAnswerOptionException>();
        session.TriviaAnswerSubmissions.Should().BeEmpty();
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().BeEmpty();
    }

    [Fact]
    public void RegisterTriviaAnswer_WhenActiveSubstageIsTreasureHunt_RejectsWithSubstageModeReason()
    {
        var session = LiveSessionFactory.CreateScheduledTriviaThenTreasureHunt();
        Activate(session);
        var team = session.Teams.First();
        var advancedAt = new DateTimeOffset(2026, 6, 3, 10, 2, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, advancedAt);
        session.CloseActiveQuestion(advancedAt.AddSeconds(30));
        session.CompleteActiveSubstageAndAdvance(advancedAt.AddSeconds(30), new SessionStateTransitionPolicy());

        var act = () => session.RegisterTriviaAnswer(team.TeamId, selectedOptionSequenceOrder: 1, Guid.NewGuid(), advancedAt.AddSeconds(31));

        act.Should().Throw<TriviaAnswerRequiresTriviaSubstageException>();
        session.TriviaAnswerSubmissions.Should().BeEmpty();
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().BeEmpty();
    }

    [Fact]
    public void RegisterTriviaAnswer_WhenActivePointerIsMissing_RejectsWithSubstageReason()
    {
        var session = ActivateTriviaSession();
        var team = session.Teams.First();
        SetPrivateProperty(session, nameof(LiveSession.ActiveSubstageId), null);

        var act = () => session.RegisterTriviaAnswer(
            team.TeamId,
            selectedOptionSequenceOrder: 1,
            Guid.NewGuid(),
            new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero));

        act.Should().Throw<TriviaAnswerRequiresTriviaSubstageException>();
        session.TriviaAnswerSubmissions.Should().BeEmpty();
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().BeEmpty();
    }

    [Fact]
    public void RegisterTriviaAnswer_WhenSessionPaused_RejectsWithSessionStateReasonAndRaisesNoEvent()
    {
        var session = ActivateTriviaSession();
        var team = session.Teams.First();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, activatedAt);
        session.MoveTo(SessionState.Paused, activatedAt.AddSeconds(5), new SessionStateTransitionPolicy());

        var act = () => session.RegisterTriviaAnswer(team.TeamId, selectedOptionSequenceOrder: 1, Guid.NewGuid(), activatedAt.AddSeconds(6));

        act.Should().Throw<TriviaAnswerRequiresActiveSessionException>();
        session.DomainEvents.OfType<AnswerRegisteredEvent>().Should().BeEmpty();
        session.DomainEvents.OfType<EvidenceSubmissionRegisteredEvent>().Should().BeEmpty();
    }

    [Fact]
    public void RegisterTriviaAnswer_WhenSessionCancelled_RejectsWithSessionStateReason()
    {
        var session = ActivateTriviaSession();
        var team = session.Teams.First();
        var activatedAt = new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, activatedAt);
        session.MoveTo(SessionState.Cancelled, activatedAt.AddSeconds(5), new SessionStateTransitionPolicy());

        var act = () => session.RegisterTriviaAnswer(team.TeamId, selectedOptionSequenceOrder: 1, Guid.NewGuid(), activatedAt.AddSeconds(6));

        act.Should().Throw<TriviaAnswerRequiresActiveSessionException>();
    }

    [Fact]
    public void RegisterTriviaAnswer_WhenSessionFinished_RejectsWithSessionStateReason()
    {
        var session = ActivateTriviaSession();
        var team = session.Teams.First();
        var at = new DateTimeOffset(2026, 6, 3, 10, 2, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, at);
        session.CloseActiveQuestion(at.AddSeconds(30));
        session.CompleteActiveSubstageAndAdvance(at.AddSeconds(30), new SessionStateTransitionPolicy());

        session.State.Should().Be(SessionState.Finished);
        var act = () => session.RegisterTriviaAnswer(team.TeamId, selectedOptionSequenceOrder: 1, Guid.NewGuid(), at.AddSeconds(31));

        act.Should().Throw<TriviaAnswerRequiresActiveSessionException>();
    }

    // ── #171: ordered substage progress on the participant team board ─────────────────────────────
    // The board exposes the whole ordered substage sequence with per-item status so a mixed-play-mode
    // participant sees where they are, not just the active substage.

    [Fact]
    public void ProjectParticipantTeamBoard_BeforeSessionIsActive_MarksEverySubstageUpcoming()
    {
        var session = LiveSessionFactory.CreateScheduledTriviaThenTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var orderedSubstages = OrderedSubstages(session);

        var board = session.ProjectParticipantTeamBoard(team.TeamId, DateTimeOffset.UtcNow);

        board.Substages.Should().HaveCount(2);
        board.Substages.Select(substage => substage.SubstageSnapshotId)
            .Should().Equal(orderedSubstages.Select(substage => substage.SubstageSnapshotId));
        board.Substages.Select(substage => substage.SequenceOrder).Should().Equal(0, 1);
        board.Substages.Select(substage => substage.Status)
            .Should().AllBeEquivalentTo(SubstageProgressStatus.Upcoming);
    }

    [Fact]
    public void ProjectParticipantTeamBoard_WhenActive_FlagsTheActiveSubstageAndKeepsLaterOnesUpcoming()
    {
        var session = LiveSessionFactory.CreateScheduledTriviaThenTreasureHunt();
        Activate(session);
        var team = session.Teams.Single();
        var orderedSubstages = OrderedSubstages(session);

        var board = session.ProjectParticipantTeamBoard(team.TeamId, DateTimeOffset.UtcNow);

        board.Substages.Select(substage => substage.PlayMode)
            .Should().Equal(SubstagePlayMode.Trivia, SubstagePlayMode.TreasureHunt);
        board.Substages[0].SubstageSnapshotId.Should().Be(orderedSubstages[0].SubstageSnapshotId);
        board.Substages[0].Status.Should().Be(SubstageProgressStatus.Active);
        board.Substages[1].Status.Should().Be(SubstageProgressStatus.Upcoming);
    }

    [Fact]
    public void ProjectParticipantTeamBoard_AfterAdvancement_MarksPriorSubstageCompletedAndNewActive()
    {
        var session = LiveSessionFactory.CreateScheduledTriviaThenTreasureHunt();
        Activate(session);
        var team = session.Teams.Single();
        var advancedAt = new DateTimeOffset(2026, 6, 3, 10, 2, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, advancedAt);
        session.CloseActiveQuestion(advancedAt.AddSeconds(30));
        session.CompleteActiveSubstageAndAdvance(advancedAt.AddSeconds(30), new SessionStateTransitionPolicy());

        var board = session.ProjectParticipantTeamBoard(team.TeamId, DateTimeOffset.UtcNow);

        board.Substages[0].Status.Should().Be(SubstageProgressStatus.Completed);
        board.Substages[1].Status.Should().Be(SubstageProgressStatus.Active);
    }

    [Fact]
    public void ProjectParticipantTeamBoard_WhenFinished_MarksEverySubstageCompleted()
    {
        var session = ActivateTriviaSession();
        var team = session.Teams.Single();
        var advancedAt = new DateTimeOffset(2026, 6, 3, 10, 2, 0, TimeSpan.Zero);
        session.ActivateQuestion(0, advancedAt);
        session.CloseActiveQuestion(advancedAt.AddSeconds(30));
        session.CompleteActiveSubstageAndAdvance(advancedAt.AddSeconds(30), new SessionStateTransitionPolicy());

        session.State.Should().Be(SessionState.Finished);
        var board = session.ProjectParticipantTeamBoard(team.TeamId, DateTimeOffset.UtcNow);

        board.Substages.Should().ContainSingle();
        board.Substages[0].Status.Should().Be(SubstageProgressStatus.Completed);
    }

    private static IReadOnlyList<SubstageSnapshot> OrderedSubstages(LiveSession session)
    {
        return session.MissionRuntimeSnapshot.StageSnapshots
            .OrderBy(stage => stage.SequenceOrder)
            .SelectMany(stage => stage.SubstageSnapshots.OrderBy(substage => substage.SequenceOrder))
            .ToList();
    }

    private static LiveSession ActivateTriviaSession()
    {
        var session = LiveSessionFactory.CreateScheduledTrivia();
        Activate(session);
        return session;
    }

    private static LiveSession ActivateTriviaSessionWithThreeQuestions()
    {
        var session = LiveSessionFactory.CreateScheduledTriviaWithThreeQuestions();
        Activate(session);
        return session;
    }

    private static LiveSession CreateTwoTreasureSubstageSessionWithTargetInSecondSubstage()
    {
        var firstSubstage = SubstageSnapshot.CreateTreasureHunt("First Route", 1);
        var secondSubstage = SubstageSnapshot.CreateTreasureHunt("Second Route", 2);
        var stage = StageSnapshot.Create("Stage One", 1, [firstSubstage, secondSubstage]);
        var firstTarget = MissionRuntimeSnapshotFactory.CreateTarget(
            firstSubstage.SubstageSnapshotId,
            "QR-FIRST");
        var secondTarget = MissionRuntimeSnapshotFactory.CreateTarget(
            secondSubstage.SubstageSnapshotId,
            "QR-LATER");
        var snapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Two Route Hunt",
            MaximumTime.Create(45),
            [stage],
            [firstTarget, secondTarget],
            []);

        return LiveSession.Create(
            SessionSource.Create(snapshot.SourceMissionId),
            "two-th",
            "Two Route Hunt",
            45,
            new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero),
            snapshot);
    }

    // Drives an already-team-associated Scheduled session to the requested state
    // through canonical edges only (used to prove team-association is Scheduled-only).
    private static void DriveTo(LiveSession session, SessionState target, SessionStateTransitionPolicy policy)
    {
        var at = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);

        switch (target)
        {
            case SessionState.Cancelled:
                session.MoveTo(SessionState.Cancelled, at, policy);
                break;
            case SessionState.Preparing:
                session.MoveTo(SessionState.Preparing, at, policy);
                break;
            case SessionState.Active:
                session.MoveTo(SessionState.Preparing, at, policy);
                session.MoveTo(SessionState.Active, at.AddMinutes(1), policy);
                break;
            case SessionState.Paused:
                session.MoveTo(SessionState.Preparing, at, policy);
                session.MoveTo(SessionState.Active, at.AddMinutes(1), policy);
                session.MoveTo(SessionState.Paused, at.AddMinutes(2), policy);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported drive target.");
        }
    }

    private static void Activate(LiveSession session)
    {
        var policy = new SessionStateTransitionPolicy();
        var preparingAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var activeAt = preparingAt.AddMinutes(1);

        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        session.MoveTo(SessionState.Preparing, preparingAt, policy);
        session.MoveTo(SessionState.Active, activeAt, policy);
    }

    private static void SetPrivateProperty(LiveSession session, string propertyName, object? value)
    {
        typeof(LiveSession)
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(session, value);
    }
}
