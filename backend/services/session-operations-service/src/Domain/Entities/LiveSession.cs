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
    private TimeSpan _sessionTimerTotalDuration;
    private TimeSpan _sessionTimerRemainingDuration;
    private DateTimeOffset? _sessionTimerAdvancingSince;
    private DateTimeOffset? _sessionTimerExpiredAt;
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
        TriviaSnapshot = null;
        _sessionTimerTotalDuration = TimeSpan.Zero;
        _sessionTimerRemainingDuration = TimeSpan.Zero;
        _questionTimerTotalDuration = TimeSpan.Zero;
        _questionTimerRemainingDuration = TimeSpan.Zero;
    }

    private LiveSession(
        Guid liveSessionId,
        SessionMode sessionMode,
        SessionSource source,
        string sessionCode,
        string titleSnapshot,
        MaximumTime maximumTime,
        DateTimeOffset scheduledAt,
        TriviaSessionSnapshot? triviaSnapshot)
    {
        if (string.IsNullOrWhiteSpace(sessionCode))
        {
            throw new LiveSessionCodeRequiredException();
        }

        if (string.IsNullOrWhiteSpace(titleSnapshot))
        {
            throw new LiveSessionTitleRequiredException();
        }

        ValidateSourceForMode(sessionMode, source);
        ValidateTriviaSnapshot(sessionMode, triviaSnapshot);

        LiveSessionId = liveSessionId;
        SessionMode = sessionMode;
        Source = source;
        SessionCode = sessionCode.Trim().ToUpperInvariant();
        TitleSnapshot = titleSnapshot.Trim();
        State = SessionState.Scheduled;
        ScheduledAt = scheduledAt;
        LastStateChangedAt = scheduledAt;
        MaximumTime = maximumTime;
        TriviaSnapshot = triviaSnapshot;
        _sessionTimerTotalDuration = TimeSpan.FromMinutes(maximumTime.Minutes);
        _sessionTimerRemainingDuration = _sessionTimerTotalDuration;
        _questionTimerTotalDuration = TimeSpan.Zero;
        _questionTimerRemainingDuration = TimeSpan.Zero;
    }

    public Guid LiveSessionId { get; private set; }

    public SessionMode SessionMode { get; private set; }

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

    public bool IsSessionTimerAdvancing => LiveSessionStateFactory.For(State).IsSessionTimerAdvancing(this);

    public bool IsQuestionTimerAdvancing => LiveSessionStateFactory.For(State).IsQuestionTimerAdvancing(this);

    public int? AssignedOperatorUserId { get; private set; }

    public TriviaSessionSnapshot? TriviaSnapshot { get; private set; }

    public int? ActiveQuestionIndex { get; private set; }

    public IReadOnlyCollection<Team> Teams => _teams.AsReadOnly();

    public IReadOnlyCollection<SessionParticipant> Participants => _participants.AsReadOnly();

    public IReadOnlyCollection<JoinContext> JoinContexts => _joinContexts.AsReadOnly();

    public static LiveSession Create(
        SessionMode sessionMode,
        SessionSource source,
        string sessionCode,
        string titleSnapshot,
        int maximumTimeMinutes,
        DateTimeOffset scheduledAt)
    {
        var session = new LiveSession(
            Guid.NewGuid(),
            sessionMode,
            source,
            sessionCode,
            titleSnapshot,
            MaximumTime.Create(maximumTimeMinutes),
            scheduledAt,
            triviaSnapshot: null);

        session.AddDomainEvent(new LiveSessionCreatedEvent(session.LiveSessionId, session.SessionCode, scheduledAt));
        return session;
    }

    public static LiveSession CreateTrivia(
        SessionSource source,
        string sessionCode,
        string titleSnapshot,
        int maximumTimeMinutes,
        DateTimeOffset scheduledAt,
        TriviaSessionSnapshot triviaSnapshot)
    {
        var session = new LiveSession(
            Guid.NewGuid(),
            SessionMode.Trivia,
            source,
            sessionCode,
            titleSnapshot,
            MaximumTime.Create(maximumTimeMinutes),
            scheduledAt,
            triviaSnapshot);

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

    public AuthoritativeSessionTimerSnapshot GetAuthoritativeSessionTimerSnapshot(DateTimeOffset observedAt)
    {
        return LiveSessionStateFactory.For(State).GetTimerSnapshot(this, observedAt);
    }

    public AuthoritativeSessionTimerSnapshot MarkSessionTimerExpiredIfElapsed(DateTimeOffset occurredAt)
    {
        return LiveSessionStateFactory.For(State).MarkTimerExpiredIfElapsed(this, occurredAt);
    }

    public void ActivateQuestion(int questionIndex, DateTimeOffset occurredAt)
    {
        EnsureCanActivateQuestion(questionIndex);

        var question = TriviaSnapshot!.Questions.ElementAt(questionIndex);
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

    internal bool HasAdvancingSessionTimer()
    {
        return _sessionTimerAdvancingSince.HasValue &&
            _sessionTimerExpiredAt is null &&
            _sessionTimerRemainingDuration > TimeSpan.Zero;
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

        if (_sessionTimerExpiredAt is not null || _sessionTimerRemainingDuration <= TimeSpan.Zero)
        {
            _sessionTimerRemainingDuration = TimeSpan.Zero;
            _sessionTimerAdvancingSince = null;
            _sessionTimerExpiredAt ??= occurredAt;
            return;
        }

        _sessionTimerAdvancingSince = occurredAt;
    }

    internal void EnterActiveQuestionTimerState(DateTimeOffset occurredAt)
    {
        ResumeQuestionTimer(occurredAt);
    }

    internal void EnterPausedSessionState(DateTimeOffset occurredAt)
    {
        FreezeSessionTimer(occurredAt);
        PausedAt = occurredAt;
    }

    internal void EnterPausedQuestionTimerState(DateTimeOffset occurredAt)
    {
        FreezeQuestionTimer(occurredAt);
    }

    internal void EnterFinishedSessionState(DateTimeOffset occurredAt)
    {
        FreezeSessionTimer(occurredAt);
        FreezeQuestionTimer(occurredAt);
        EndedAt = occurredAt;
    }

    internal void EnterCancelledSessionState(DateTimeOffset occurredAt)
    {
        FreezeSessionTimer(occurredAt);
        FreezeQuestionTimer(occurredAt);
        CancelledAt = occurredAt;
    }

    internal AuthoritativeSessionTimerSnapshot GetAdvancingSessionTimerSnapshot(DateTimeOffset observedAt)
    {
        var remaining = CalculateAdvancingSessionTimerRemaining(observedAt);
        var expired = _sessionTimerExpiredAt.HasValue || remaining <= TimeSpan.Zero;

        return AuthoritativeSessionTimerSnapshot.Create(
            _sessionTimerTotalDuration,
            remaining,
            isAdvancing: !expired && HasAdvancingSessionTimer(),
            observedAt,
            _sessionTimerAdvancingSince,
            _sessionTimerExpiredAt);
    }

    internal AuthoritativeSessionTimerSnapshot GetFrozenSessionTimerSnapshot(DateTimeOffset observedAt)
    {
        return AuthoritativeSessionTimerSnapshot.Create(
            _sessionTimerTotalDuration,
            _sessionTimerExpiredAt.HasValue ? TimeSpan.Zero : _sessionTimerRemainingDuration,
            isAdvancing: false,
            observedAt,
            advancingSince: null,
            _sessionTimerExpiredAt);
    }

    internal AuthoritativeSessionTimerSnapshot MarkAdvancingSessionTimerExpiredIfElapsed(DateTimeOffset occurredAt)
    {
        var remaining = CalculateAdvancingSessionTimerRemaining(occurredAt);
        if (remaining > TimeSpan.Zero)
        {
            return GetAdvancingSessionTimerSnapshot(occurredAt);
        }

        _sessionTimerRemainingDuration = TimeSpan.Zero;
        _sessionTimerAdvancingSince = null;
        _sessionTimerExpiredAt ??= occurredAt;

        return GetFrozenSessionTimerSnapshot(occurredAt);
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

    private void FreezeSessionTimer(DateTimeOffset occurredAt)
    {
        if (_sessionTimerAdvancingSince is null)
        {
            return;
        }

        _sessionTimerRemainingDuration = CalculateAdvancingSessionTimerRemaining(occurredAt);
        _sessionTimerAdvancingSince = null;

        if (_sessionTimerRemainingDuration <= TimeSpan.Zero)
        {
            _sessionTimerRemainingDuration = TimeSpan.Zero;
            _sessionTimerExpiredAt ??= occurredAt;
        }
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

    private TimeSpan CalculateAdvancingSessionTimerRemaining(DateTimeOffset observedAt)
    {
        if (_sessionTimerExpiredAt.HasValue)
        {
            return TimeSpan.Zero;
        }

        if (_sessionTimerAdvancingSince is null)
        {
            return _sessionTimerRemainingDuration;
        }

        var elapsed = observedAt - _sessionTimerAdvancingSince.Value;
        if (elapsed <= TimeSpan.Zero)
        {
            return _sessionTimerRemainingDuration;
        }

        var remaining = _sessionTimerRemainingDuration - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
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

        if (TriviaSnapshot is null)
        {
            throw new TriviaSessionSnapshotRequiredException();
        }

        if (questionIndex < 0 || questionIndex >= TriviaSnapshot.Questions.Count)
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

    private Team? FindAssignedTeam(Guid sessionParticipantId)
    {
        return _teams.SingleOrDefault(team =>
            team.Members.Any(member =>
                member.SessionParticipantId == sessionParticipantId &&
                member.IsActive));
    }

    private static void ValidateSourceForMode(SessionMode sessionMode, SessionSource source)
    {
        var expectedType = sessionMode == SessionMode.TreasureHunt
            ? SessionSourceType.Mission
            : SessionSourceType.TriviaQuiz;

        if (source.SourceType != expectedType)
        {
            throw new SessionSourceDoesNotMatchModeException(sessionMode, source.SourceType);
        }
    }

    private static void ValidateTriviaSnapshot(SessionMode sessionMode, TriviaSessionSnapshot? triviaSnapshot)
    {
        if (sessionMode == SessionMode.Trivia && triviaSnapshot is null)
        {
            throw new TriviaSessionSnapshotRequiredException();
        }
    }
}
