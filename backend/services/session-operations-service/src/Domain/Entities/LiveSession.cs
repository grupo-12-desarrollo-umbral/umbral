using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

public sealed class LiveSession : BaseAuditableEntity
{
    private readonly List<Team> _teams = new();
    private readonly List<SessionParticipant> _participants = new();
    private readonly List<JoinContext> _joinContexts = new();

    private LiveSession()
    {
        LiveSessionId = Guid.Empty;
        SessionCode = string.Empty;
        TitleSnapshot = string.Empty;
        Source = null!;
        MaximumTime = null!;
    }

    private LiveSession(
        Guid liveSessionId,
        SessionMode sessionMode,
        SessionSource source,
        string sessionCode,
        string titleSnapshot,
        MaximumTime maximumTime,
        DateTimeOffset scheduledAt)
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

        LiveSessionId = liveSessionId;
        SessionMode = sessionMode;
        Source = source;
        SessionCode = sessionCode.Trim().ToUpperInvariant();
        TitleSnapshot = titleSnapshot.Trim();
        State = SessionState.Scheduled;
        ScheduledAt = scheduledAt;
        LastStateChangedAt = scheduledAt;
        MaximumTime = maximumTime;
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

    public int? AssignedOperatorUserId { get; private set; }

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
            scheduledAt);

        session.AddDomainEvent(new LiveSessionCreatedEvent(session.LiveSessionId, session.SessionCode, scheduledAt));
        return session;
    }

    public Team RegisterTeam(string displayName, string teamCode)
    {
        var normalizedCode = TeamCode.Create(teamCode);
        if (_teams.Any(team => team.TeamCode == normalizedCode))
        {
            throw new DuplicateTeamCodeInSessionException(normalizedCode.Value);
        }

        var team = Team.Register(LiveSessionId, displayName, normalizedCode.Value);
        _teams.Add(team);
        AddDomainEvent(new TeamRegisteredInSessionEvent(LiveSessionId, team.TeamId, team.TeamCode.Value));
        return team;
    }

    public (SessionParticipant Participant, Team Team, bool IsReconnect) AdmitParticipant(
        Guid externalIdentityId,
        string displayName,
        Guid teamId,
        DateTimeOffset occurredAt,
        int teamCapacity,
        JoinPolicy joinPolicy)
    {
        ArgumentNullException.ThrowIfNull(joinPolicy);

        var team = GetTeam(teamId);
        var existingParticipant = _participants.SingleOrDefault(participant => participant.ExternalIdentityId == externalIdentityId);

        if (existingParticipant is null)
        {
            joinPolicy.EnsureCanJoin(this, team, teamCapacity);

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

        joinPolicy.EnsureCanReconnect(this, existingParticipant, existingTeam, teamId);
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

        transitionPolicy.EnsureCanTransition(State, nextState, _teams.Count);

        var previousState = State;
        State = nextState;
        LastStateChangedAt = occurredAt;
        StateReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        switch (nextState)
        {
            case SessionState.Active:
                StartedAt ??= occurredAt;
                PausedAt = null;
                break;
            case SessionState.Paused:
                PausedAt = occurredAt;
                break;
            case SessionState.Finished:
                EndedAt = occurredAt;
                break;
            case SessionState.Cancelled:
                CancelledAt = occurredAt;
                break;
        }

        AddDomainEvent(new SessionStateChangedEvent(LiveSessionId, previousState, nextState, occurredAt));
    }

    private Team GetTeam(Guid teamId)
    {
        return _teams.SingleOrDefault(team => team.TeamId == teamId)
            ?? throw new TeamNotFoundException(teamId);
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
}
