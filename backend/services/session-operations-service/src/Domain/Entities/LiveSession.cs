using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.Services.SessionStates;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

public sealed class LiveSession : BaseAuditableEntity
{
    private readonly List<Team> _teams = new();
    private readonly List<SessionParticipant> _participants = new();
    private readonly List<JoinContext> _joinContexts = new();
    private TimeSpan _questionTimerTotalDuration;
    private TimeSpan _questionTimerRemainingDuration;
    private DateTimeOffset? _questionTimerAdvancingSince;
    private DateTimeOffset? _questionTimerExpiredAt;

    private LiveSession()
    {
        LiveSessionId = Guid.Empty;
        SessionCode = string.Empty;
        TitleSnapshot = string.Empty;
        Source = null!;
        MaximumTime = null!;
        MissionRuntimeSnapshot = null!;
        _questionTimerTotalDuration = TimeSpan.Zero;
        _questionTimerRemainingDuration = TimeSpan.Zero;
    }

    private LiveSession(
        Guid liveSessionId,
        SessionSource source,
        string sessionCode,
        string titleSnapshot,
        MaximumTime maximumTime,
        DateTimeOffset scheduledAt,
        MissionRuntimeSnapshot missionRuntimeSnapshot,
        int? assignedOperatorUserId)
    {
        if (string.IsNullOrWhiteSpace(sessionCode))
        {
            throw new LiveSessionCodeRequiredException();
        }

        if (string.IsNullOrWhiteSpace(titleSnapshot))
        {
            throw new LiveSessionTitleRequiredException();
        }

        ValidateSource(source);

        LiveSessionId = liveSessionId;
        Source = source;
        SessionCode = sessionCode.Trim().ToUpperInvariant();
        TitleSnapshot = titleSnapshot.Trim();
        State = SessionState.Scheduled;
        ScheduledAt = scheduledAt;
        LastStateChangedAt = scheduledAt;
        MaximumTime = maximumTime;
        MissionRuntimeSnapshot = missionRuntimeSnapshot;
        AssignedOperatorUserId = assignedOperatorUserId;
        _questionTimerTotalDuration = TimeSpan.Zero;
        _questionTimerRemainingDuration = TimeSpan.Zero;
    }

    public Guid LiveSessionId { get; private set; }

    public SessionSource Source { get; private set; }

    public string SessionCode { get; private set; }

    public string TitleSnapshot { get; private set; }

    public SessionState State { get; private set; }

    public DateTimeOffset ScheduledAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? PausedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public DateTimeOffset LastStateChangedAt { get; private set; }

    public string? StateReason { get; private set; }

    public MaximumTime MaximumTime { get; private set; }

    public bool IsQuestionTimerAdvancing => LiveSessionStateFactory.For(State).IsQuestionTimerAdvancing(this);

    public int? AssignedOperatorUserId { get; private set; }

    public MissionRuntimeSnapshot MissionRuntimeSnapshot { get; private set; }

    public int? ActiveQuestionIndex { get; private set; }

    // The single authoritative live-substage pointer (ADR-0005): walks strict stage->substage
    // order; null before the session is Active. Question activation/close and advancement are all
    // scoped to this substage.
    public Guid? ActiveSubstageId { get; private set; }

    public IReadOnlyCollection<Team> Teams => _teams.AsReadOnly();

    public IReadOnlyCollection<SessionParticipant> Participants => _participants.AsReadOnly();

    public IReadOnlyCollection<JoinContext> JoinContexts => _joinContexts.AsReadOnly();

    public static LiveSession Create(
        SessionSource source,
        string sessionCode,
        string titleSnapshot,
        int maximumTimeMinutes,
        DateTimeOffset scheduledAt,
        MissionRuntimeSnapshot missionRuntimeSnapshot,
        int? assignedOperatorUserId = null)
    {
        var session = new LiveSession(
            Guid.NewGuid(),
            source,
            sessionCode,
            titleSnapshot,
            MaximumTime.Create(maximumTimeMinutes),
            scheduledAt,
            missionRuntimeSnapshot,
            assignedOperatorUserId);

        session.AddDomainEvent(new LiveSessionCreatedEvent(session.LiveSessionId, session.SessionCode, scheduledAt));
        return session;
    }

    public bool HasAssociatedTeams => _teams.Any(team => team.ReferenceTeamId.HasValue);

    public int AssociatedTeamCount => _teams.Count(team => team.ReferenceTeamId.HasValue);

    public Team RegisterTeam(string displayName, string teamCode, int capacity)
    {
        return RegisterTeam(Guid.NewGuid(), displayName, teamCode, capacity);
    }

    public Team RegisterTeam(Guid teamId, string displayName, string teamCode, int capacity)
    {
        var normalizedCode = TeamCode.Create(teamCode);
        if (_teams.Any(team => team.TeamCode == normalizedCode))
        {
            throw new DuplicateTeamCodeInSessionException(normalizedCode.Value);
        }

        var team = Team.Register(LiveSessionId, teamId, displayName, normalizedCode.Value, capacity);
        _teams.Add(team);
        AddDomainEvent(new TeamRegisteredInSessionEvent(LiveSessionId, team.TeamId, team.TeamCode.Value));
        return team;
    }

    public Team AssociateTeam(Guid referenceTeamId, string displayName, string teamCode, int capacity)
    {
        EnsureCanAssociateTeam();

        if (_teams.Any(team => team.ReferenceTeamId == referenceTeamId))
        {
            throw new DuplicateTeamAssociationInSessionException(referenceTeamId);
        }

        var normalizedCode = TeamCode.Create(teamCode);
        if (_teams.Any(team => team.TeamCode == normalizedCode))
        {
            throw new DuplicateTeamCodeInSessionException(normalizedCode.Value);
        }

        var team = Team.Associate(LiveSessionId, referenceTeamId, displayName, normalizedCode.Value, capacity);
        _teams.Add(team);
        AddDomainEvent(new TeamRegisteredInSessionEvent(LiveSessionId, team.TeamId, team.TeamCode.Value));
        return team;
    }

    public (SessionParticipant Participant, Team Team, bool IsReconnect) AdmitParticipant(
        Guid externalIdentityId,
        string displayName,
        Guid teamId,
        DateTimeOffset occurredAt,
        JoinPolicy joinPolicy)
    {
        ArgumentNullException.ThrowIfNull(joinPolicy);

        var team = GetTeam(teamId);
        var existingParticipant = _participants.SingleOrDefault(participant => participant.ExternalIdentityId == externalIdentityId);

        if (existingParticipant is null)
        {
            joinPolicy.EnsureCanJoin(this, team);

            var participant = SessionParticipant.Join(LiveSessionId, externalIdentityId, displayName, occurredAt);
            _participants.Add(participant);
            participant.MarkActive(occurredAt);
            team.AssignParticipant(participant, occurredAt);

            AddDomainEvent(new ParticipantJoinedSessionEvent(LiveSessionId, participant.SessionParticipantId, team.TeamId, occurredAt));
            AddDomainEvent(new ParticipantAssignedToTeamEvent(LiveSessionId, participant.SessionParticipantId, team.TeamId, occurredAt));
            return (participant, team, false);
        }

        var existingTeam = FindAssignedTeam(existingParticipant.SessionParticipantId);
        if (existingTeam is null)
        {
            throw new ParticipantRemovedFromSessionException(existingParticipant.SessionParticipantId);
        }

        joinPolicy.EnsureCanReconnect(this, existingParticipant, existingTeam, team.TeamId);
        existingParticipant.RefreshPresence(occurredAt);
        return (existingParticipant, existingTeam, true);
    }

    // Pre-start team pick / switch (Open Team Selection write path, #88 policy + #89 rules). A
    // participant self-assigns into an attached team, or switches from their current pick to another.
    // The #88 policy gates pre-start availability + authorized set; here we add the #89 rules:
    // capacity-checked on the team being joined, and switching out frees the old slot (decisions §8).
    // Freezes automatically once the session leaves Scheduled/Preparing — the policy throws.
    public (SessionParticipant Participant, Team Team) SelectTeam(
        Guid externalIdentityId,
        string displayName,
        Guid targetTeamId,
        IReadOnlySet<Guid> authorizedReferenceTeamIds,
        DateTimeOffset occurredAt,
        OpenTeamSelectionPolicy openTeamSelectionPolicy)
    {
        ArgumentNullException.ThrowIfNull(openTeamSelectionPolicy);

        var target = GetTeam(targetTeamId);
        openTeamSelectionPolicy.EnsureCanSelfAssign(this, target, authorizedReferenceTeamIds);

        var participant = _participants.SingleOrDefault(participant => participant.ExternalIdentityId == externalIdentityId);
        var currentTeam = participant is null ? null : FindAssignedTeam(participant.SessionParticipantId);

        if (currentTeam is not null && currentTeam.TeamId == target.TeamId)
        {
            return (participant!, currentTeam); // Re-picking the current team is a no-op.
        }

        // Capacity is checked on the team being joined (§8); the participant does not yet hold a slot
        // there, so a free slot is required. The old slot is released only after this passes.
        if (target.ActiveMemberCount >= target.Capacity)
        {
            throw new TeamCapacityReachedException(target.TeamId, target.Capacity);
        }

        if (participant is null)
        {
            participant = SessionParticipant.Join(LiveSessionId, externalIdentityId, displayName, occurredAt);
            _participants.Add(participant);
        }

        currentTeam?.ReleaseParticipant(participant.SessionParticipantId, occurredAt);
        target.AssignParticipant(participant, occurredAt);

        AddDomainEvent(new ParticipantAssignedToTeamEvent(LiveSessionId, participant.SessionParticipantId, target.TeamId, occurredAt));
        return (participant, target);
    }

    public void DisconnectParticipant(Guid participantId, DateTimeOffset occurredAt)
    {
        var participant = _participants.Single(participant => participant.SessionParticipantId == participantId);
        participant.Disconnect(occurredAt);
    }

    public JoinContext OpenJoinContext(Guid? teamId, Guid? joinTokenId, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        var joinContext = JoinContext.Create(LiveSessionId, teamId, joinTokenId, createdAt, expiresAt);
        _joinContexts.Add(joinContext);
        return joinContext;
    }

    public void MoveTo(SessionState nextState, DateTimeOffset occurredAt, SessionStateTransitionPolicy transitionPolicy, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(transitionPolicy);

        transitionPolicy.EnsureCanTransition(State, nextState, AssociatedTeamCount);

        var previousState = State;
        State = nextState;
        LastStateChangedAt = occurredAt;
        StateReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        LiveSessionStateFactory.For(nextState).Enter(this, occurredAt);

        AddDomainEvent(new SessionStateChangedEvent(LiveSessionId, previousState, nextState, occurredAt));
    }

    // Authoritative displayed remaining time = the active trivia-question window (OD-1/OD-2/OD-3):
    // a trivia question active -> the TriviaQuestionTimer window; otherwise no advancing countdown.
    public AuthoritativeSessionTimerSnapshot GetAuthoritativeSessionTimerSnapshot(DateTimeOffset observedAt)
    {
        return GetActiveQuestionTimerSnapshot(observedAt);
    }

    public void ActivateQuestion(int questionIndex, DateTimeOffset occurredAt)
    {
        EnsureCanActivateQuestion(questionIndex);

        var question = GetOrderedTriviaQuestions().ElementAt(questionIndex);
        ActiveQuestionIndex = questionIndex;
        _questionTimerTotalDuration = TimeSpan.FromSeconds(question.TimeLimitSeconds);
        _questionTimerRemainingDuration = _questionTimerTotalDuration;
        _questionTimerExpiredAt = null;
        _questionTimerAdvancingSince = occurredAt;

        AddDomainEvent(new QuestionActivatedEvent(
            LiveSessionId,
            questionIndex,
            question.SequenceOrder,
            question.TimeLimitSeconds,
            occurredAt));
    }

    public AuthoritativeSessionTimerSnapshot GetActiveQuestionTimerSnapshot(DateTimeOffset observedAt)
    {
        return LiveSessionStateFactory.For(State).GetQuestionTimerSnapshot(this, observedAt);
    }

    public AuthoritativeSessionTimerSnapshot MarkQuestionTimerExpiredIfElapsed(DateTimeOffset occurredAt)
    {
        return LiveSessionStateFactory.For(State).MarkQuestionTimerExpiredIfElapsed(this, occurredAt);
    }

    public void CloseActiveQuestion(DateTimeOffset occurredAt)
    {
        if (ActiveQuestionIndex is null)
        {
            throw new NoActiveQuestionException();
        }

        var questionIndex = ActiveQuestionIndex.Value;
        var wasExpiredByTimer = _questionTimerExpiredAt.HasValue ||
            CalculateAdvancingQuestionTimerRemaining(occurredAt) <= TimeSpan.Zero;

        FreezeQuestionTimer(occurredAt);
        ActiveQuestionIndex = null;

        AddDomainEvent(new QuestionClosedEvent(
            LiveSessionId,
            questionIndex,
            occurredAt,
            wasExpiredByTimer));
    }

    // Timer-driven, generic substage advancement (ADR-0005): once the active substage's last
    // question has closed, walk to the next substage in strict stage->substage order. A next
    // substage exists -> move the pointer and raise SubstageAdvancedEvent (the facade activates the
    // first question for a trivia substage; a treasure-hunt substage parks, D-4). No next substage
    // -> SessionCompletion: Finished is reached ONLY here, never operator-forced.
    public void CompleteActiveSubstageAndAdvance(DateTimeOffset occurredAt, SessionStateTransitionPolicy transitionPolicy)
    {
        ArgumentNullException.ThrowIfNull(transitionPolicy);
        LiveSessionStateFactory.For(State).EnsureCanAdvanceSubstage(this);

        if (ActiveSubstageId is null)
        {
            throw new NoActiveSubstageException();
        }

        var orderedSubstages = GetOrderedSubstages();
        var currentIndex = Array.FindIndex(orderedSubstages, substage => substage.SubstageSnapshotId == ActiveSubstageId.Value);
        var fromSubstage = orderedSubstages[currentIndex];
        var nextSubstage = currentIndex + 1 < orderedSubstages.Length ? orderedSubstages[currentIndex + 1] : null;

        if (nextSubstage is null)
        {
            AddDomainEvent(new SubstageAdvancedEvent(
                LiveSessionId,
                fromSubstage.SubstageSnapshotId,
                fromSubstage.PlayMode,
                toSubstageId: null,
                occurredAt));
            MoveTo(SessionState.Finished, occurredAt, transitionPolicy);
            return;
        }

        ActiveSubstageId = nextSubstage.SubstageSnapshotId;
        AddDomainEvent(new SubstageAdvancedEvent(
            LiveSessionId,
            fromSubstage.SubstageSnapshotId,
            fromSubstage.PlayMode,
            nextSubstage.SubstageSnapshotId,
            occurredAt));
    }

    public void AssignOperator(int operatorUserId, DateTimeOffset occurredAt)
    {
        if (operatorUserId <= 0)
        {
            throw new OperatorUserIdMustBePositiveException();
        }

        var previousOperatorUserId = AssignedOperatorUserId;
        AssignedOperatorUserId = operatorUserId;

        AddDomainEvent(new LiveSessionOperatorAssignedEvent(
            LiveSessionId,
            previousOperatorUserId,
            AssignedOperatorUserId,
            occurredAt));
    }

    internal bool HasAdvancingQuestionTimer()
    {
        return ActiveQuestionIndex.HasValue &&
            _questionTimerAdvancingSince.HasValue &&
            _questionTimerExpiredAt is null &&
            _questionTimerRemainingDuration > TimeSpan.Zero;
    }

    internal void EnterActiveSessionState(DateTimeOffset occurredAt)
    {
        StartedAt ??= occurredAt;
        PausedAt = null;

        // Entering Active starts the first substage in strict order (CONTEXT.md:48). `??=` guards
        // pause->resume so resuming never rewinds the pointer to the first substage.
        ActiveSubstageId ??= GetOrderedSubstages()[0].SubstageSnapshotId;
    }

    internal void EnterActiveQuestionTimerState(DateTimeOffset occurredAt)
    {
        ResumeQuestionTimer(occurredAt);
    }

    internal void EnterPausedSessionState(DateTimeOffset occurredAt)
    {
        PausedAt = occurredAt;
    }

    internal void EnterPausedQuestionTimerState(DateTimeOffset occurredAt)
    {
        FreezeQuestionTimer(occurredAt);
    }

    internal void EnterFinishedSessionState(DateTimeOffset occurredAt)
    {
        FreezeQuestionTimer(occurredAt);
        EndedAt = occurredAt;
    }

    internal void EnterCancelledSessionState(DateTimeOffset occurredAt)
    {
        FreezeQuestionTimer(occurredAt);
        CancelledAt = occurredAt;
    }

    internal AuthoritativeSessionTimerSnapshot GetAdvancingQuestionTimerSnapshot(DateTimeOffset observedAt)
    {
        var remaining = CalculateAdvancingQuestionTimerRemaining(observedAt);
        var expired = _questionTimerExpiredAt.HasValue || remaining <= TimeSpan.Zero;

        return AuthoritativeSessionTimerSnapshot.Create(
            _questionTimerTotalDuration,
            remaining,
            isAdvancing: !expired && HasAdvancingQuestionTimer(),
            observedAt,
            _questionTimerAdvancingSince,
            _questionTimerExpiredAt);
    }

    internal AuthoritativeSessionTimerSnapshot GetFrozenQuestionTimerSnapshot(DateTimeOffset observedAt)
    {
        return AuthoritativeSessionTimerSnapshot.Create(
            _questionTimerTotalDuration,
            _questionTimerExpiredAt.HasValue ? TimeSpan.Zero : _questionTimerRemainingDuration,
            isAdvancing: false,
            observedAt,
            advancingSince: null,
            _questionTimerExpiredAt);
    }

    internal AuthoritativeSessionTimerSnapshot MarkAdvancingQuestionTimerExpiredIfElapsed(DateTimeOffset occurredAt)
    {
        var remaining = CalculateAdvancingQuestionTimerRemaining(occurredAt);
        if (remaining > TimeSpan.Zero)
        {
            return GetAdvancingQuestionTimerSnapshot(occurredAt);
        }

        _questionTimerRemainingDuration = TimeSpan.Zero;
        _questionTimerAdvancingSince = null;
        _questionTimerExpiredAt ??= occurredAt;

        return GetFrozenQuestionTimerSnapshot(occurredAt);
    }

    private void ResumeQuestionTimer(DateTimeOffset occurredAt)
    {
        if (ActiveQuestionIndex is null)
        {
            return;
        }

        if (_questionTimerExpiredAt is not null || _questionTimerRemainingDuration <= TimeSpan.Zero)
        {
            _questionTimerRemainingDuration = TimeSpan.Zero;
            _questionTimerAdvancingSince = null;
            _questionTimerExpiredAt ??= occurredAt;
            return;
        }

        _questionTimerAdvancingSince = occurredAt;
    }

    private void FreezeQuestionTimer(DateTimeOffset occurredAt)
    {
        if (_questionTimerAdvancingSince is null)
        {
            return;
        }

        _questionTimerRemainingDuration = CalculateAdvancingQuestionTimerRemaining(occurredAt);
        _questionTimerAdvancingSince = null;

        if (_questionTimerRemainingDuration <= TimeSpan.Zero)
        {
            _questionTimerRemainingDuration = TimeSpan.Zero;
            _questionTimerExpiredAt ??= occurredAt;
        }
    }

    private TimeSpan CalculateAdvancingQuestionTimerRemaining(DateTimeOffset observedAt)
    {
        if (_questionTimerExpiredAt.HasValue)
        {
            return TimeSpan.Zero;
        }

        if (_questionTimerAdvancingSince is null)
        {
            return _questionTimerRemainingDuration;
        }

        var elapsed = observedAt - _questionTimerAdvancingSince.Value;
        if (elapsed <= TimeSpan.Zero)
        {
            return _questionTimerRemainingDuration;
        }

        var remaining = _questionTimerRemainingDuration - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private void EnsureCanActivateQuestion(int questionIndex)
    {
        if (State is not SessionState.Active)
        {
            throw new QuestionActivationRequiresActiveSessionException(State);
        }

        if (ActiveQuestionIndex is not null)
        {
            throw new QuestionAlreadyActiveException(ActiveQuestionIndex.Value);
        }

        var orderedTriviaQuestions = GetOrderedTriviaQuestions();
        if (orderedTriviaQuestions.Length == 0)
        {
            throw new TriviaSubstageSnapshotMustContainQuestionsException();
        }

        if (questionIndex < 0 || questionIndex >= orderedTriviaQuestions.Length)
        {
            throw new QuestionIndexOutOfRangeException(questionIndex);
        }
    }

    private Team GetTeam(Guid teamId)
    {
        return _teams.SingleOrDefault(team =>
                team.TeamId == teamId ||
                team.ReferenceTeamId == teamId)
            ?? throw new TeamNotFoundException(teamId);
    }

    private void EnsureCanAssociateTeam()
    {
        if (State != SessionState.Scheduled)
        {
            throw new TeamAssociationRequiresScheduledSessionException(State);
        }
    }

    // Read accessor for the lobby (#108): the attached team the external participant currently holds
    // an active membership in, or null. Maps ExternalIdentityId → the private assigned-team lookup.
    public Team? FindTeamForExternalParticipant(Guid externalIdentityId)
    {
        var participant = _participants.SingleOrDefault(participant => participant.ExternalIdentityId == externalIdentityId);
        return participant is null ? null : FindAssignedTeam(participant.SessionParticipantId);
    }

    private Team? FindAssignedTeam(Guid sessionParticipantId)
    {
        return _teams.SingleOrDefault(team =>
            team.Members.Any(member =>
                member.SessionParticipantId == sessionParticipantId &&
                member.IsActive));
    }

    // Questions of the ACTIVE substage only, in SequenceOrder — the scope every consumer shares
    // (ActivateQuestion, EnsureCanActivateQuestion, the strategy). Empty before the session is
    // Active (no active substage).
    private TriviaQuestionSnapshot[] GetOrderedTriviaQuestions()
    {
        if (ActiveSubstageId is null)
        {
            return [];
        }

        return MissionRuntimeSnapshot.TriviaQuestionSnapshots
            .Where(question => question.SubstageSnapshotId == ActiveSubstageId.Value)
            .OrderBy(question => question.SequenceOrder)
            .ToArray();
    }

    private SubstageSnapshot[] GetOrderedSubstages()
    {
        return MissionRuntimeSnapshot.StageSnapshots
            .OrderBy(stage => stage.SequenceOrder)
            .SelectMany(stage => stage.SubstageSnapshots.OrderBy(substage => substage.SequenceOrder))
            .ToArray();
    }

    private static void ValidateSource(SessionSource source)
    {
        if (source.SourceType != SessionSourceType.Mission)
        {
            throw new SessionSourceEntityRequiredException();
        }
    }
}
