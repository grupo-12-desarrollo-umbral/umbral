using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.Services.SessionStates;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

public sealed class LiveSession : BaseAuditableEntity
{
    private const string HiddenUntilOperatorReleasePolicy = "HiddenUntilOperatorRelease";

    // How long every substage's ranking stays on screen before the session advances or finishes (D-3).
    // Lives on the domain, not on TriviaRoundOrchestratorFacade beside QuestionRevealDuration: this
    // window is mode-agnostic and a treasure hunt opens it from RegisterTargetScan, with no facade in
    // the call path. The two reveals are different mechanisms at different granularities and both
    // survive — 5s per question (trivia only), then 10s per substage (both modes).
    public static readonly TimeSpan SubstageRankingRevealDuration = TimeSpan.FromSeconds(10);

    private readonly List<Team> _teams = new();
    private readonly List<SessionParticipant> _participants = new();
    private readonly List<JoinContext> _joinContexts = new();
    private readonly List<TriviaAnswerSubmission> _triviaAnswerSubmissions = new();
    private readonly List<TreasureEvidenceSubmission> _treasureEvidenceSubmissions = new();
    private readonly List<SessionEvent> _sessionEvents = new();
    private readonly List<ClueReleaseRecord> _clueReleaseRecords = new();
    private readonly List<OperativeClue> _operativeClues = new();
    private TimeSpan _questionTimerTotalDuration;
    private TimeSpan _questionTimerRemainingDuration;
    private DateTimeOffset? _questionTimerAdvancingSince;
    private DateTimeOffset? _questionTimerExpiredAt;
    private TimeSpan _missionTimerTotalDuration;
    private TimeSpan _missionTimerRemainingDuration;
    private DateTimeOffset? _missionTimerAdvancingSince;
    private DateTimeOffset? _missionTimerExpiredAt;
    // Post-close reveal window (HU-35): a just-closed trivia question stays "revealing" until this
    // deadline, so participants see the correct option/result before the next question activates.
    // Null when no reveal is pending. `_pendingNextQuestionIndex` holds the deferred activation the
    // reveal end will perform (null => the substage is exhausted and must advance instead).
    private DateTimeOffset? _questionRevealUntil;
    private int? _pendingNextQuestionIndex;
    // Substage-end ranking reveal (D-3): the substage has ended — cleared by a team (treasure hunt) or
    // out of questions (trivia) — and every team sees the ranking until this deadline, after which the
    // session advances or finishes. Null when no substage reveal is pending. Distinct from
    // `_questionRevealUntil` above: that one is per-question and trivia-only, this one is per-substage
    // and mode-agnostic. An absolute deadline, so a pause does not extend it (see the spec's step-3
    // decision 2) — a paused session is simply not ticked and advances on the first tick after resume.
    private DateTimeOffset? _substageRevealUntil;

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
        _missionTimerTotalDuration = TimeSpan.Zero;
        _missionTimerRemainingDuration = TimeSpan.Zero;
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
        _missionTimerTotalDuration = TimeSpan.Zero;
        _missionTimerRemainingDuration = TimeSpan.Zero;
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

    public bool IsMissionTimerAdvancing => LiveSessionStateFactory.For(State).IsMissionTimerAdvancing(this);

    // False until the session starts: the mission deadline is seeded from MaximumTime on the
    // Scheduled -> Active transition, so before that there is no deadline to report.
    public bool HasMissionDeadline => _missionTimerTotalDuration > TimeSpan.Zero;

    // True while a just-closed trivia question is showing its result (HU-35 reveal window). During
    // this window ActiveQuestionIndex is null (the question is closed) but the next activation is
    // deferred until `_questionRevealUntil`.
    public bool IsAwaitingQuestionReveal => _questionRevealUntil.HasValue;

    public DateTimeOffset? QuestionRevealUntil => _questionRevealUntil;

    // True while a just-ended substage is showing its ranking (D-3). Both play modes reach this: a
    // treasure hunt when the first team clears (D-1), a trivia substage when its last question's own
    // reveal completes. The substage pointer does not move until the reveal ends.
    public bool IsAwaitingSubstageRankingReveal => _substageRevealUntil.HasValue;

    public DateTimeOffset? SubstageRevealUntil => _substageRevealUntil;

    // The activation deferred by the reveal window: the substage-local index of the next question to
    // open when the reveal ends, or null when the just-closed question was the substage's last (advance
    // instead). Resolved against the still-active question at close, so mid-substage it is the just-closed
    // question's index + 1 — which is exactly the exclusive upper bound of the now-readable results.
    public int? PendingNextQuestionIndex => _pendingNextQuestionIndex;

    public bool IsQuestionRevealElapsed(DateTimeOffset observedAt) =>
        _questionRevealUntil is { } until && observedAt >= until;

    public bool IsSubstageRevealElapsed(DateTimeOffset observedAt) =>
        _substageRevealUntil is { } until && observedAt >= until;

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

    public IReadOnlyCollection<SessionEvent> SessionEvents => _sessionEvents.AsReadOnly();

    // Accepted trivia answers (base evidence + trivia specialization). Only first-write-wins accepted
    // answers live here; rejected attempts throw and never enter this collection.
    public IReadOnlyCollection<TriviaAnswerSubmission> TriviaAnswerSubmissions => _triviaAnswerSubmissions.AsReadOnly();

    public IReadOnlyCollection<TreasureEvidenceSubmission> TreasureEvidenceSubmissions => _treasureEvidenceSubmissions.AsReadOnly();

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

    // An unknown identity here is a first join, not a reconnect: it self-assigns into the requested team.
    // That makes it the same decision SelectTeam makes, so it must clear the same authorized set — otherwise
    // reconnect becomes a way to obtain a membership Open Team Selection would refuse. Callers holding the
    // participant's whitelist pass it with openTeamSelectionPolicy; an empty/omitted set keeps the documented
    // "unassigned => every attached team is selectable" semantics.
    public (SessionParticipant Participant, Team Team, bool IsReconnect) AdmitParticipant(
        Guid externalIdentityId,
        string displayName,
        Guid teamId,
        DateTimeOffset occurredAt,
        JoinPolicy joinPolicy,
        OpenTeamSelectionPolicy? openTeamSelectionPolicy = null,
        IReadOnlySet<Guid>? authorizedReferenceTeamIds = null)
    {
        ArgumentNullException.ThrowIfNull(joinPolicy);

        var team = GetTeam(teamId);
        var existingParticipant = _participants.SingleOrDefault(participant => participant.ExternalIdentityId == externalIdentityId);

        if (existingParticipant is null)
        {
            // Ordered before the authorized-set gate so state/capacity keep reporting late-join and
            // team-full ahead of a whitelist verdict.
            joinPolicy.EnsureCanJoin(this, team);
            openTeamSelectionPolicy?.EnsureCanSelfAssign(
                this,
                team,
                authorizedReferenceTeamIds ?? new HashSet<Guid>());

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

    // Hard disconnect (operator/seed): forces the participant offline and clears every held connection.
    public void DisconnectParticipant(Guid participantId, DateTimeOffset occurredAt)
    {
        var participant = _participants.Single(participant => participant.SessionParticipantId == participantId);
        participant.Disconnect(occurredAt);
    }

    // Registers a socket (ConnectionId) under a participant as part of the serialized aggregate write.
    // Because every UpdateAsync forces a principal-row xmin bump (Phase 2), this admission and any
    // overlapping disconnect of another socket are serialized against each other rather than racing.
    public void RegisterParticipantConnection(Guid participantId, string connectionId, DateTimeOffset occurredAt)
    {
        var participant = _participants.Single(participant => participant.SessionParticipantId == participantId);
        participant.RegisterConnection(connectionId, occurredAt);
    }

    // Socket-lifecycle disconnect, keyed on ConnectionId: idempotent and decrement-guarded (see
    // SessionParticipant.DropConnection). Tolerant of an absent participant so a late disconnect
    // callback for a participant that was already removed is a harmless no-op.
    public void DisconnectParticipantConnection(Guid participantId, string connectionId, DateTimeOffset occurredAt)
    {
        var participant = _participants.SingleOrDefault(participant => participant.SessionParticipantId == participantId);
        participant?.DropConnection(connectionId, occurredAt);
    }

    public JoinContext OpenJoinContext(Guid? teamId, Guid? joinTokenId, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        var joinContext = JoinContext.Create(LiveSessionId, teamId, joinTokenId, createdAt, expiresAt);
        _joinContexts.Add(joinContext);
        return joinContext;
    }

    public void MoveTo(
        SessionState nextState,
        DateTimeOffset occurredAt,
        SessionStateTransitionPolicy transitionPolicy,
        string? reason = null,
        int? responsibleUserId = null,
        Guid? responsibleUserExternalId = null)
    {
        ArgumentNullException.ThrowIfNull(transitionPolicy);

        transitionPolicy.EnsureCanTransition(State, nextState, AssociatedTeamCount);

        ApplyStateChange(nextState, occurredAt, reason, responsibleUserId, responsibleUserExternalId);
    }

    // Shared state-change application, deliberately split out of MoveTo so SessionCompletion (see
    // CompleteActiveSubstageAndAdvance) can reach Finished without going through the generic
    // transition policy: CanTransitionTo intentionally excludes Finished from Active/Paused so no
    // Operator-facing path can request it (per canon, Finished is reached ONLY via SessionCompletion).
    private void ApplyStateChange(
        SessionState nextState,
        DateTimeOffset occurredAt,
        string? reason,
        int? responsibleUserId,
        Guid? responsibleUserExternalId)
    {
        var previousState = State;
        State = nextState;
        LastStateChangedAt = occurredAt;
        StateReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        var actorType = responsibleUserId.HasValue
            ? SessionEventActorType.Operator
            : SessionEventActorType.System;

        LiveSessionStateFactory.For(nextState).Enter(this, occurredAt);

        _sessionEvents.Add(SessionEvent.ForStateChange(
            LiveSessionId,
            previousState,
            nextState,
            occurredAt,
            actorType,
            responsibleUserId,
            StateReason));

        AddDomainEvent(new SessionStateChangedEvent(
            LiveSessionId,
            previousState,
            nextState,
            occurredAt,
            responsibleUserId,
            StateReason,
            actorType,
            responsibleUserExternalId));
    }

    // Authoritative displayed remaining time is selected by the active substage's play mode: a
    // treasure-hunt substage has no window of its own, so it shows the mission deadline; Trivia keeps
    // the active-question window.
    public AuthoritativeSessionTimerSnapshot GetAuthoritativeSessionTimerSnapshot(DateTimeOffset observedAt)
    {
        if (ActiveSubstageId is null)
        {
            return GetActiveQuestionTimerSnapshot(observedAt);
        }

        var activeSubstage = GetOrderedSubstages()
            .SingleOrDefault(substage => substage.SubstageSnapshotId == ActiveSubstageId.Value);

        return activeSubstage?.PlayMode == SubstagePlayMode.TreasureHunt
            ? GetMissionTimerSnapshot(observedAt)
            : GetActiveQuestionTimerSnapshot(observedAt);
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

    public AuthoritativeSessionTimerSnapshot GetMissionTimerSnapshot(DateTimeOffset observedAt)
    {
        return LiveSessionStateFactory.For(State).GetMissionTimerSnapshot(this, observedAt);
    }

    public AuthoritativeSessionTimerSnapshot MarkQuestionTimerExpiredIfElapsed(DateTimeOffset occurredAt)
    {
        return LiveSessionStateFactory.For(State).MarkQuestionTimerExpiredIfElapsed(this, occurredAt);
    }

    public AuthoritativeSessionTimerSnapshot MarkMissionTimerExpiredIfElapsed(DateTimeOffset occurredAt)
    {
        return LiveSessionStateFactory.For(State).MarkMissionTimerExpiredIfElapsed(this, occurredAt);
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

    // Close the active question AND open the reveal window (HU-35). The next activation is deferred:
    // `nextQuestionIndex` (resolved against the still-active question BEFORE this call) is captured so
    // the reveal end can activate it, or advance the substage when null (the substage is exhausted).
    // The worker fires `CompleteQuestionReveal` once `occurredAt + revealDuration` elapses.
    public void CloseActiveQuestionForReveal(
        DateTimeOffset occurredAt,
        TimeSpan revealDuration,
        int? nextQuestionIndex)
    {
        CloseActiveQuestion(occurredAt);
        _questionRevealUntil = occurredAt + revealDuration;
        _pendingNextQuestionIndex = nextQuestionIndex;
    }

    // End the reveal window and hand back the deferred activation captured at close. Returns the next
    // question index to activate, or null when the substage is exhausted and must advance. Idempotent
    // callers should guard on `IsAwaitingQuestionReveal` first.
    public int? CompleteQuestionRevealAndDequeueNext()
    {
        if (_questionRevealUntil is null)
        {
            throw new NoActiveQuestionRevealException();
        }

        var nextQuestionIndex = _pendingNextQuestionIndex;
        _questionRevealUntil = null;
        _pendingNextQuestionIndex = null;
        return nextQuestionIndex;
    }

    public void ReleaseClueToTeam(
        ClueReleaseSubject subject,
        Guid teamId,
        int operatorUserId,
        DateTimeOffset now)
    {
        EnsureSessionActiveForClueRelease();
        EnsureOperatorUserIdIsValid(operatorUserId);
        ResolveReleasableSubject(subject);
        var team = GetTeam(teamId);
        EnsureClueNotAlreadyReleased(team.TeamId, subject);
        AppendManualClueRelease(subject, team, operatorUserId, now);
    }

    public void ReleaseClueToAllTeams(
        ClueReleaseSubject subject,
        int operatorUserId,
        DateTimeOffset now)
    {
        EnsureSessionActiveForClueRelease();
        EnsureOperatorUserIdIsValid(operatorUserId);
        ResolveReleasableSubject(subject);

        foreach (var team in _teams)
        {
            EnsureClueNotAlreadyReleased(team.TeamId, subject);
        }

        foreach (var team in _teams)
        {
            AppendManualClueRelease(subject, team, operatorUserId, now);
        }
    }

    public IReadOnlyCollection<ClueReleaseRecord> GetClueReleaseRecords()
    {
        return _clueReleaseRecords.AsReadOnly();
    }

    public void AddOperativeClue(
        string clueText,
        IReadOnlyCollection<Guid> teamIds,
        int operatorUserId,
        DateTimeOffset now)
    {
        if (State is not (SessionState.Active or SessionState.Paused))
        {
            throw new SessionNotLiveForOperativeClueException(State);
        }

        if (string.IsNullOrWhiteSpace(clueText))
        {
            throw new OperativeClueTextRequiredException();
        }

        if (teamIds.Count == 0)
        {
            throw new OperativeClueRequiresAtLeastOneTeamException();
        }

        EnsureOperatorUserIdIsValid(operatorUserId);
        var teams = teamIds.Select(GetTeam).ToArray();
        var trimmedClueText = clueText.Trim();

        foreach (var team in teams)
        {
            var operativeClue = OperativeClue.Create(
                LiveSessionId,
                team.TeamId,
                trimmedClueText,
                operatorUserId,
                now);

            _operativeClues.Add(operativeClue);
            _sessionEvents.Add(SessionEvent.ForOperativeClueAdded(
                LiveSessionId,
                now,
                operatorUserId,
                team.TeamId,
                trimmedClueText));
            AddDomainEvent(new OperativeClueAddedEvent(
                operativeClue.OperativeClueId,
                operativeClue.LiveSessionId,
                operativeClue.TeamId,
                operativeClue.ClueText,
                operativeClue.CreatedByUserId,
                operativeClue.CreatedAt));
        }
    }

    public IReadOnlyCollection<OperativeClue> GetOperativeClues()
    {
        return _operativeClues.AsReadOnly();
    }

    // HU-36A restricted pre-close monitor: for the active synchronized trivia question, enumerate the
    // session's teams and mark each answered iff an accepted TriviaAnswerSubmission exists for
    // (TeamId, ActiveSubstageId, QuestionSequenceOrder). Teams with none render not-answered — derived
    // from absence, not a stored flag. Rejects (via ResolveActiveTriviaQuestion) when there is no active
    // trivia question / the active substage is not Trivia. The returned snapshot/status types carry no
    // option/correctness/score, so the chosen option cannot leak before the question closes.
    public TriviaAnsweredMonitorSnapshot ProjectActiveQuestionAnsweredStatus()
    {
        var question = ResolveActiveTriviaQuestion();

        var teamStatuses = _teams
            .OrderBy(team => team.TeamCode.Value, StringComparer.Ordinal)
            .Select(team => BuildTeamAnsweredStatus(team, question))
            .ToList();

        return TriviaAnsweredMonitorSnapshot.Create(question.SubstageSnapshotId, question.SequenceOrder, teamStatuses);
    }

    private TeamAnsweredStatus BuildTeamAnsweredStatus(Team team, TriviaQuestionSnapshot question)
    {
        var acceptedAnswer = _triviaAnswerSubmissions.SingleOrDefault(answer =>
            answer.TeamId == team.TeamId &&
            answer.ActiveSubstageId == question.SubstageSnapshotId &&
            answer.QuestionSequenceOrder == question.SequenceOrder);

        return acceptedAnswer is null
            ? TeamAnsweredStatus.CreateNotAnswered(team.TeamId, team.TeamCode.Value, team.DisplayName)
            : TeamAnsweredStatus.CreateAnswered(team.TeamId, team.TeamCode.Value, team.DisplayName, acceptedAnswer.SubmittedAt);
    }

    // Shared evidence registration with the existing trivia specialization.
    // One stable ordered workflow governs BOTH outcomes: the first in-time answer is ACCEPTED and every
    // late/duplicate/invalid attempt is REJECTED. Accept and reject are two branches of THIS single
    // write — the guards below (window + first-write-wins) are the only divergence points; there is no
    // separate reject entry point and no second entity/event for rejected attempts. The invariant steps
    // stay in this one place while the generic prefix is shared by every concrete evidence form.
    public TriviaAnswerSubmission RegisterTriviaAnswer(
        Guid teamId,
        int selectedOptionSequenceOrder,
        Guid submittedByParticipantId,
        DateTimeOffset submittedAt)
    {
        try
        {
            var submission = RegisterEvidenceCore(
                teamId,
                EvidenceSubmissionType.TriviaAnswer,
                submittedByParticipantId,
                submittedAt,
                (team, _, participantId, registeredAt) =>
                {
                    var question = ResolveActiveTriviaQuestion();
                    EnsureAnswerWindowOpen(registeredAt);
                    var selectedOption = ResolveSelectedOption(question, selectedOptionSequenceOrder);
                    EnsureFirstAnswerWins(team.TeamId, question);

                    return BeginTriviaAnswer(
                        team,
                        question,
                        selectedOption,
                        participantId!.Value,
                        registeredAt);
                });

            submission.AcceptRegisteredAnswer(submittedAt);
            _triviaAnswerSubmissions.Add(submission);
            RaiseAnswerRegistered(submission);
            return submission;
        }
        catch (EvidenceSubmissionContextRequiredException) when (ActiveSubstageId is null)
        {
            throw new TriviaAnswerRequiresTriviaSubstageException();
        }
    }

    public TreasureEvidenceSubmission RegisterTargetScan(
        Guid teamId,
        string scannedValue,
        Guid submittedByParticipantId,
        DateTimeOffset submittedAt)
    {
        // Pure read, so it is safe ahead of the state gates below; a duplicate QR code yields more
        // than one match and deliberately resolves to no target.
        var matches = MatchScannedTargets(scannedValue);

        var submission = RegisterEvidenceCore(
            teamId,
            EvidenceSubmissionType.TreasureHuntQrScan,
            submittedByParticipantId,
            submittedAt,
            (team, activeSubstageId, participantId, registeredAt) =>
            {
                EnsureActiveTreasureHuntSubstage(activeSubstageId);
                var resolvedTarget = matches.Count == 1 ? matches[0] : null;

                return TreasureEvidenceSubmission.Begin(
                    LiveSessionId,
                    team.TeamId,
                    activeSubstageId,
                    scannedValue,
                    resolvedTarget?.TargetSnapshotId,
                    participantId!.Value,
                    registeredAt);
            });

        _treasureEvidenceSubmissions.Add(submission);

        var target = submission.TargetSnapshotId is null
            ? null
            : MissionRuntimeSnapshot.TargetSnapshots.Single(target =>
                target.TargetSnapshotId == submission.TargetSnapshotId.Value);

        var rejectionReason = DetermineTargetResolutionRejection(submission, target, matches.Count > 1);
        if (rejectionReason is not null)
        {
            submission.RejectRegisteredTarget(rejectionReason.Value, submittedAt);
            return submission;
        }

        submission.AcceptRegisteredTarget(submittedAt);
        var resolvedTeam = GetTeam(submission.TeamId);
        AddDomainEvent(new TargetResolvedEvent(
            LiveSessionId,
            submission.TeamId,
            resolvedTeam.ReferenceTeamId ?? resolvedTeam.TeamId,
            resolvedTeam.DisplayName,
            submission.EvidenceSubmissionId,
            submission.ActiveSubstageId,
            target!.TargetSnapshotId,
            target.Score,
            submission.SubmittedAt));

        // D-1: this scan may have just cleared the substage for everyone. Checked after the accept
        // above, so the target that completes the set is counted and scored like any other.
        if (IsSubstageClearedBy(submission.TeamId, submission.ActiveSubstageId))
        {
            BeginSubstageRankingReveal(submittedAt, SubstageRankingRevealDuration);
        }

        return submission;
    }

    private void EnsureActiveTreasureHuntSubstage(Guid activeSubstageId)
    {
        var activeSubstage = GetOrderedSubstages().SingleOrDefault(substage =>
            substage.SubstageSnapshotId == activeSubstageId);

        if (activeSubstage?.PlayMode != SubstagePlayMode.TreasureHunt)
        {
            throw new EvidenceSubmissionContextRequiredException();
        }
    }

    // Returns every snapshotted target matching the scanned code. Authoring enforces mission-scoped
    // QR uniqueness, but a mission snapshotted before that guard can still hold duplicates, so this
    // reports the ambiguity rather than throwing on a second match.
    private IReadOnlyList<TargetSnapshot> MatchScannedTargets(string scannedValue)
    {
        var normalizedValue = scannedValue.Trim();
        return MissionRuntimeSnapshot.TargetSnapshots
            .Where(target => string.Equals(target.QrCode, normalizedValue, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private TargetResolutionRejectionReason? DetermineTargetResolutionRejection(
        TreasureEvidenceSubmission submission,
        TargetSnapshot? target,
        bool isAmbiguous)
    {
        if (target is null)
        {
            // An ambiguous scan resolves to no single target; report it as such rather than
            // pretending the code is unknown.
            return isAmbiguous
                ? TargetResolutionRejectionReason.ScannedValueResolvesToMultipleTargets
                : TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget;
        }

        if (target.SubstageSnapshotId != submission.ActiveSubstageId || !target.IsActive)
        {
            return TargetResolutionRejectionReason.TargetOutsideActiveSubstage;
        }

        var alreadyResolved = _treasureEvidenceSubmissions.Any(existing =>
            existing != submission &&
            existing.TeamId == submission.TeamId &&
            existing.TargetSnapshotId == target.TargetSnapshotId &&
            existing.ValidationState == EvidenceValidationState.Accepted);

        if (alreadyResolved)
        {
            return TargetResolutionRejectionReason.TargetAlreadyResolvedByTeam;
        }

        // D-2 — hard cut. The substage is closed and its ranking is on screen, so a scan that would
        // otherwise have resolved cannot: this is the in-flight scan the clearing team beat. Retained
        // for audit like any rejection, and every already-accepted target keeps its score.
        //
        // Checked LAST so the more specific diagnoses above still win. This is also what makes the
        // reason truthful rather than merely close: the team that cleared resolved every target, so its
        // own later scans are duplicates caught above — only a team that did NOT clear can reach here,
        // which is exactly what "another team already completed this stage" claims.
        if (_substageRevealUntil.HasValue)
        {
            return TargetResolutionRejectionReason.SubstageAlreadyCleared;
        }

        return null;
    }

    // Step 1 — session-state gate, delegated to the State type (Active is the only state that admits
    // answers; Paused/Finished/Cancelled and pre-start states reject).
    internal TSubmission RegisterEvidenceCore<TSubmission>(
        Guid teamId,
        EvidenceSubmissionType submissionType,
        Guid? submittedByParticipantId,
        DateTimeOffset submittedAt,
        Func<Team, Guid, Guid?, DateTimeOffset, TSubmission> registerConcreteForm)
        where TSubmission : EvidenceSubmission
    {
        EnsureSessionAdmitsEvidence();
        var team = GetTeam(teamId);
        var activeSubstageId = ResolveActiveSubstageForEvidence();
        var submission = registerConcreteForm(team, activeSubstageId, submittedByParticipantId, submittedAt);

        AddDomainEvent(new EvidenceSubmissionRegisteredEvent(
            LiveSessionId,
            team.TeamId,
            submission.EvidenceSubmissionId,
            activeSubstageId,
            submissionType,
            submittedAt,
            EvidenceValidationState.Pending,
            originReference: submission.DescribeOrigin()));

        return submission;
    }

    private void EnsureSessionAdmitsEvidence()
    {
        LiveSessionStateFactory.For(State).EnsureCanRegisterEvidence(this);
    }

    private Guid ResolveActiveSubstageForEvidence()
    {
        if (ActiveSubstageId is null ||
            GetOrderedSubstages().All(substage => substage.SubstageSnapshotId != ActiveSubstageId.Value))
        {
            throw new EvidenceSubmissionContextRequiredException();
        }

        return ActiveSubstageId.Value;
    }

    // Step 3 — the synchronized active trivia question shared by all teams (HU-33A seam). Rejects a
    // non-trivia active substage, then the absence of an active question.
    private TriviaQuestionSnapshot ResolveActiveTriviaQuestion()
    {
        var activeSubstage = ActiveSubstageId is null
            ? null
            : GetOrderedSubstages().SingleOrDefault(substage => substage.SubstageSnapshotId == ActiveSubstageId.Value);

        if (activeSubstage is null || activeSubstage.PlayMode != SubstagePlayMode.Trivia)
        {
            throw new TriviaAnswerRequiresTriviaSubstageException();
        }

        if (ActiveQuestionIndex is null)
        {
            throw new TriviaAnswerRequiresActiveQuestionException();
        }

        return GetOrderedTriviaQuestions().ElementAt(ActiveQuestionIndex.Value);
    }

    // Step 4 — the answer is in time only while the active question's timer window is still open. Keys
    // off the same expiry the question-close path uses, not a wall clock (HU-22).
    private void EnsureAnswerWindowOpen(DateTimeOffset submittedAt)
    {
        var windowClosed = _questionTimerExpiredAt.HasValue ||
            CalculateAdvancingQuestionTimerRemaining(submittedAt) <= TimeSpan.Zero;

        if (windowClosed)
        {
            throw new LateTriviaAnswerException();
        }
    }

    // Step 5 — the selected option must be one of the question snapshot's options (identified by
    // sequence order; the frozen snapshot carries no per-option Guid).
    private static TriviaOptionSnapshot ResolveSelectedOption(TriviaQuestionSnapshot question, int selectedOptionSequenceOrder)
    {
        return question.Options.SingleOrDefault(option => option.SequenceOrder == selectedOptionSequenceOrder)
            ?? throw new InvalidTriviaAnswerOptionException(selectedOptionSequenceOrder);
    }

    // Step 6 — first-write-wins: exactly one accepted answer per team per snapshotted question. A repeat
    // is rejected as a duplicate. This is the guard that makes accept/reject one write.
    private void EnsureFirstAnswerWins(Guid teamId, TriviaQuestionSnapshot question)
    {
        var alreadyAnswered = _triviaAnswerSubmissions.Any(answer =>
            answer.TeamId == teamId &&
            answer.ActiveSubstageId == question.SubstageSnapshotId &&
            answer.QuestionSequenceOrder == question.SequenceOrder);

        if (alreadyAnswered)
        {
            throw new DuplicateTriviaAnswerException(teamId, question.SequenceOrder);
        }
    }

    // Step 7 — create the base evidence + trivia specialization, snapshotting correctness and the
    // awarded score from the question option (correct → question ScoreValue; wrong → zero).
    private TriviaAnswerSubmission BeginTriviaAnswer(
        Team team,
        TriviaQuestionSnapshot question,
        TriviaOptionSnapshot selectedOption,
        Guid submittedByParticipantId,
        DateTimeOffset submittedAt)
    {
        var isCorrect = selectedOption.IsCorrect;
        var scoreValue = isCorrect ? question.ScoreValue : 0;

        return TriviaAnswerSubmission.Begin(
            LiveSessionId,
            team.TeamId,
            question.SubstageSnapshotId,
            question.SequenceOrder,
            selectedOption.SequenceOrder,
            submittedByParticipantId,
            submittedAt,
            isCorrect,
            scoreValue);
    }

    // Step 8 — the accepted-answer fact, raised only on the success path.
    private void RaiseAnswerRegistered(TriviaAnswerSubmission submission)
    {
        var answeringTeam = GetTeam(submission.TeamId);
        AddDomainEvent(new AnswerRegisteredEvent(
            LiveSessionId,
            submission.TeamId,
            answeringTeam.ReferenceTeamId ?? answeringTeam.TeamId,
            answeringTeam.DisplayName,
            submission.EvidenceSubmissionId,
            submission.ActiveSubstageId,
            submission.QuestionSequenceOrder,
            submission.SelectedOptionSequenceOrder,
            submission.IsCorrect,
            submission.ScoreValue,
            submission.SubmittedAt));
    }

    // Team-scoped board projection (HU-23): for the given team, returns current score, the
    // authoritative timer snapshot, active-substage target progress (treasure-hunt) or active-question
    // context (trivia), and optional visible clues. Progress is target-resolution-based, never
    // clue-based. Score is session-owned or zero; no score ledger/ranking computation.
    public ParticipantTeamBoardSnapshot ProjectParticipantTeamBoard(Guid teamId, DateTimeOffset observedAt)
    {
        var team = GetTeam(teamId);
        var timerSnapshot = GetAuthoritativeSessionTimerSnapshot(observedAt);
        var activeSubstageContext = BuildActiveSubstageContext(team.TeamId);
        var substages = BuildSubstageProgress();
        var visibleClues = CollectVisibleClues(team.TeamId);
        var activeTargets = CollectActiveTargets();

        return ParticipantTeamBoardSnapshot.Create(
            team.TeamId,
            team.DisplayName,
            team.TeamCode.Value,
            team.CurrentScore ?? 0,
            timerSnapshot,
            activeSubstageContext,
            substages,
            visibleClues,
            activeTargets);
    }

    public OperatorSessionPanelSnapshot ProjectOperatorSessionPanel(DateTimeOffset observedAt)
    {
        var timerSnapshot = GetAuthoritativeSessionTimerSnapshot(observedAt);
        // Substage-initial clues are session-level (same for every team) and never increment the
        // persisted per-team ReleasedClueCount, so add them to each team's manual-release tally here.
        var substageInitialClueCount = CountVisibleSubstageInitialClues();
        var sharedContexts = new Dictionary<(SubstagePlayMode? PlayMode, int ResolvedTargets), ActiveSubstageContext?>();
        var teamProgress = _teams
            .OrderBy(team => team.TeamCode.Value, StringComparer.Ordinal)
            .Select(team =>
            {
                var context = BuildActiveSubstageContext(team.TeamId);
                var contextKey = (context?.PlayMode, context?.ResolvedTargets ?? 0);
                if (!sharedContexts.TryGetValue(contextKey, out var sharedContext))
                {
                    sharedContext = context;
                    sharedContexts.Add(contextKey, sharedContext);
                }

                return OperatorTeamProgress.Create(
                    team.TeamId,
                    team.ReferenceTeamId,
                    team.TeamCode.Value,
                    team.DisplayName,
                    team.CurrentScore ?? 0,
                    team.ReleasedClueCount + substageInitialClueCount,
                    sharedContext);
            })
            .ToList();

        return OperatorSessionPanelSnapshot.Create(
            LiveSessionId,
            State,
            timerSnapshot,
            teamProgress);
    }

    // Currently-visible VisibleWhenSubstageStarts initial clues on the active substage. Trivia-only —
    // treasure-hunt clues live per-target, not in the substage-scoped ClueSnapshots — so this is 0 for
    // a treasure-hunt or inactive substage. Deliberately counts only initial clues: released hidden
    // clues already increment each team's ReleasedClueCount and would otherwise be counted twice.
    private int CountVisibleSubstageInitialClues()
    {
        if (ActiveSubstageId is null)
        {
            return 0;
        }

        var activeSubstage = GetOrderedSubstages()
            .SingleOrDefault(substage => substage.SubstageSnapshotId == ActiveSubstageId.Value);

        return activeSubstage?.PlayMode == SubstagePlayMode.Trivia
            ? MissionRuntimeSnapshot.ClueSnapshots.Count(clue =>
                clue.SubstageSnapshotId == activeSubstage.SubstageSnapshotId &&
                clue.IsVisibleWhenSubstageStarts &&
                !string.IsNullOrWhiteSpace(clue.Text))
            : 0;
    }

    // Operator release-clue picker: hidden clues in the active substage, target-backed for treasure
    // hunts and clue-snapshot-backed for trivia. Empty when no active substage has releasable clues.
    public IReadOnlyList<ReleasableClue> ProjectReleasableClues()
    {
        if (ActiveSubstageId is null)
        {
            return [];
        }

        var activeSubstage = GetOrderedSubstages()
            .SingleOrDefault(substage => substage.SubstageSnapshotId == ActiveSubstageId.Value);

        if (activeSubstage is null)
        {
            return [];
        }

        if (activeSubstage.PlayMode == SubstagePlayMode.TreasureHunt)
        {
            return MissionRuntimeSnapshot.TargetSnapshots
                .Where(target =>
                    target.SubstageSnapshotId == activeSubstage.SubstageSnapshotId &&
                    target.IsActive &&
                    !string.IsNullOrWhiteSpace(target.ClueText) &&
                    string.Equals(
                        target.ClueVisibilityPolicy,
                        HiddenUntilOperatorReleasePolicy,
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(target => target.SequenceOrder)
                .Select(target => ReleasableClue.ForTarget(
                    target.TargetSnapshotId,
                    target.Name,
                    target.SequenceOrder,
                    target.ClueText!))
                .ToList();
        }

        return MissionRuntimeSnapshot.ClueSnapshots
            .Where(clue =>
                clue.SubstageSnapshotId == activeSubstage.SubstageSnapshotId &&
                clue.IsHiddenUntilOperatorRelease &&
                !string.IsNullOrWhiteSpace(clue.Text))
            .OrderBy(clue => clue.SequenceOrder)
            .Select(clue => ReleasableClue.ForSubstageClue(
                clue.ClueSnapshotId,
                clue.SequenceOrder,
                clue.Text))
            .ToList();
    }

    // Projects the whole ordered substage sequence with per-item progress status (#171). Status is
    // derived from each substage's position vs. the live-substage pointer: before the session is
    // Active nothing has started (all Upcoming); once Finished the pointer is gone and everything is
    // behind us (all Completed); otherwise it splits Completed / Active / Upcoming around the pointer.
    // SequenceOrder is the flattened session-wide position, so the whole cross-stage flow reads in one
    // monotonic order regardless of per-stage numbering.
    private IReadOnlyList<SubstageProgressItem> BuildSubstageProgress()
    {
        var orderedSubstages = GetOrderedSubstages();
        var activeIndex = ActiveSubstageId is null
            ? -1
            : Array.FindIndex(orderedSubstages, substage => substage.SubstageSnapshotId == ActiveSubstageId.Value);

        return orderedSubstages
            .Select((substage, index) => new SubstageProgressItem(
                substage.SubstageSnapshotId,
                substage.Title,
                index,
                substage.PlayMode,
                DeriveSubstageProgressStatus(index, activeIndex)))
            .ToList();
    }

    private SubstageProgressStatus DeriveSubstageProgressStatus(int index, int activeIndex)
    {
        // Pre-Active states have no live substage yet — the whole flow is still ahead.
        if (State is SessionState.Scheduled or SessionState.Preparing)
        {
            return SubstageProgressStatus.Upcoming;
        }

        // SessionCompletion finishes the session without rewinding the pointer (it parks on the last
        // substage), so Finished is the authoritative "everything is behind us" signal — treat the
        // whole sequence as Completed rather than leaving the final substage flagged Active.
        if (State is SessionState.Finished || activeIndex < 0)
        {
            return SubstageProgressStatus.Completed;
        }

        if (index < activeIndex)
        {
            return SubstageProgressStatus.Completed;
        }

        return index == activeIndex ? SubstageProgressStatus.Active : SubstageProgressStatus.Upcoming;
    }

    private ActiveSubstageContext? BuildActiveSubstageContext(Guid teamId)
    {
        if (ActiveSubstageId is null)
        {
            return null;
        }

        var activeSubstage = GetOrderedSubstages()
            .SingleOrDefault(substage => substage.SubstageSnapshotId == ActiveSubstageId.Value);

        if (activeSubstage is null)
        {
            return null;
        }

        return activeSubstage.PlayMode == SubstagePlayMode.TreasureHunt
            ? BuildTreasureHuntContext(activeSubstage, teamId)
            : BuildTriviaContext(activeSubstage);
    }

    // D-1: a treasure-hunt substage is cleared by the first team to resolve every one of its active
    // targets. The two counts below are the same pair the team board reads (BuildTreasureHuntContext),
    // shared from here so a clear can never disagree with the board that displays it.
    //
    // Zero active targets deliberately does NOT count as cleared: such a substage is unreachable by
    // scanning and must be rejected at authoring instead (D-7). A "zero => instantly cleared" fallback
    // here would silently paper over a mission that a human should be told to fix.
    public bool IsSubstageClearedBy(Guid teamId, Guid substageId)
    {
        var activeTargets = CountActiveTargets(substageId);
        return activeTargets > 0 && CountResolvedTargets(teamId, substageId) == activeTargets;
    }

    private int CountActiveTargets(Guid substageId) =>
        MissionRuntimeSnapshot.TargetSnapshots
            .Count(target => target.SubstageSnapshotId == substageId && target.IsActive);

    private int CountResolvedTargets(Guid teamId, Guid substageId) =>
        _treasureEvidenceSubmissions.Count(submission =>
            submission.TeamId == teamId &&
            submission.ActiveSubstageId == substageId &&
            submission.ValidationState == EvidenceValidationState.Accepted);

    private ActiveSubstageContext BuildTreasureHuntContext(SubstageSnapshot substage, Guid teamId)
    {
        var activeTargets = MissionRuntimeSnapshot.TargetSnapshots
            .Where(target => target.SubstageSnapshotId == substage.SubstageSnapshotId && target.IsActive)
            .OrderBy(target => target.SequenceOrder)
            .Select(target => new ActiveSubstageTarget(
                target.TargetSnapshotId,
                target.Name,
                target.SequenceOrder,
                string.Equals(
                    target.ClueVisibilityPolicy,
                    HiddenUntilOperatorReleasePolicy,
                    StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var resolvedTargets = CountResolvedTargets(teamId, substage.SubstageSnapshotId);

        return ActiveSubstageContext.CreateTreasureHunt(
            substage.SubstageSnapshotId,
            substage.Title,
            resolvedTargets,
            activeTargets);
    }

    private ActiveSubstageContext BuildTriviaContext(SubstageSnapshot substage)
    {
        int? activeQuestionSequenceOrder = null;
        int? activeQuestionTimeLimitSeconds = null;

        if (ActiveQuestionIndex is not null)
        {
            var orderedQuestions = GetOrderedTriviaQuestions();
            if (ActiveQuestionIndex.Value < orderedQuestions.Length)
            {
                var activeQuestion = orderedQuestions[ActiveQuestionIndex.Value];
                activeQuestionSequenceOrder = activeQuestion.SequenceOrder;
                activeQuestionTimeLimitSeconds = activeQuestion.TimeLimitSeconds;
            }
        }

        return ActiveSubstageContext.CreateTrivia(
            substage.SubstageSnapshotId,
            substage.Title,
            activeQuestionSequenceOrder,
            activeQuestionTimeLimitSeconds);
    }

    private IReadOnlyList<VisibleClue> CollectVisibleClues(Guid teamId)
    {
        IReadOnlyList<VisibleClue> plannedClues = [];
        if (ActiveSubstageId is not null)
        {
            var activeSubstage = GetOrderedSubstages()
                .SingleOrDefault(substage => substage.SubstageSnapshotId == ActiveSubstageId.Value);

            if (activeSubstage is not null)
            {
                // Treasure-hunt clues resolve per-target (unchanged). A trivia substage has no targets, so
                // its clues resolve from the substage-scoped clue snapshot instead (#145).
                plannedClues = activeSubstage.PlayMode == SubstagePlayMode.TreasureHunt
                    ? CollectTargetVisibleClues(activeSubstage.SubstageSnapshotId, teamId)
                    : CollectSubstageVisibleClues(activeSubstage.SubstageSnapshotId, teamId);
            }
        }

        // Operative clues render newest-authored-first: they are live operator guidance, so the freshest
        // is the most actionable. Order explicitly rather than leaning on insertion order — the backing
        // collection is loaded with a plain `.Include("_operativeClues")` (no ORDER BY) and keys on a
        // random Guid, so the load order carries no chronology. CreatedAt cannot tie within a group: a
        // push stamps one clue per team, so a given team never gets two at the same instant.
        return plannedClues
            .Concat(_operativeClues
                .Where(clue => clue.TeamId == teamId)
                .OrderByDescending(clue => clue.CreatedAt)
                .Select(clue => VisibleClue.CreateForOperative(clue.OperativeClueId, clue.ClueText)))
            .ToList();
    }

    private IReadOnlyList<VisibleClue> CollectTargetVisibleClues(Guid substageSnapshotId, Guid teamId)
    {
        // Two-group order: always-visible clues (policy != HiddenUntilOperatorRelease) are pinned at the
        // top by SequenceOrder ascending, then operator-released hidden clues render below them
        // newest-released-first. SequenceOrder breaks ties within the released group (an all-teams release
        // stamps every team's record with the same instant), keeping same-instant releases stably ordered.
        return MissionRuntimeSnapshot.TargetSnapshots
            .Where(target =>
                target.SubstageSnapshotId == substageSnapshotId &&
                target.IsActive &&
                !string.IsNullOrWhiteSpace(target.ClueText) &&
                !string.IsNullOrWhiteSpace(target.ClueVisibilityPolicy))
            .Select(target => new
            {
                target,
                release = _clueReleaseRecords.SingleOrDefault(record =>
                    record.TeamId == teamId &&
                    record.TargetId == target.TargetSnapshotId),
                alwaysVisible = !string.Equals(
                    target.ClueVisibilityPolicy,
                    HiddenUntilOperatorReleasePolicy,
                    StringComparison.OrdinalIgnoreCase),
            })
            .Where(entry => entry.alwaysVisible || entry.release is not null)
            .OrderBy(entry => entry.alwaysVisible ? 0 : 1)
            .ThenByDescending(entry =>
                entry.alwaysVisible ? DateTimeOffset.MinValue : entry.release!.ReleasedAt)
            .ThenBy(entry => entry.target.SequenceOrder)
            .Select(entry => VisibleClue.Create(
                entry.target.TargetSnapshotId,
                entry.target.ClueText!,
                entry.target.Name))
            .ToList();
    }

    private void EnsureSessionActiveForClueRelease()
    {
        if (State is not SessionState.Active)
        {
            throw new SessionNotActiveForClueReleaseException(State);
        }
    }

    private static void EnsureOperatorUserIdIsValid(int operatorUserId)
    {
        if (operatorUserId <= 0)
        {
            throw new OperatorUserIdMustBePositiveException();
        }
    }

    private TargetSnapshot ResolveReleasableTarget(Guid targetId)
    {
        if (ActiveSubstageId is null)
        {
            throw new ClueNotReleasableException(targetId);
        }

        var activeSubstage = GetOrderedSubstages()
            .SingleOrDefault(substage => substage.SubstageSnapshotId == ActiveSubstageId.Value);

        if (activeSubstage is null || activeSubstage.PlayMode != SubstagePlayMode.TreasureHunt)
        {
            throw new ClueNotReleasableException(targetId);
        }

        return MissionRuntimeSnapshot.TargetSnapshots.SingleOrDefault(target =>
                target.TargetSnapshotId == targetId &&
                target.SubstageSnapshotId == activeSubstage.SubstageSnapshotId &&
                target.IsActive &&
                !string.IsNullOrWhiteSpace(target.ClueText) &&
                string.Equals(
                    target.ClueVisibilityPolicy,
                    HiddenUntilOperatorReleasePolicy,
                    StringComparison.OrdinalIgnoreCase))
            ?? throw new ClueNotReleasableException(targetId);
    }

    private ClueSnapshot ResolveReleasableSubstageClue(Guid clueId)
    {
        if (ActiveSubstageId is null)
        {
            throw new ClueNotReleasableException(clueId);
        }

        var activeSubstage = GetOrderedSubstages()
            .SingleOrDefault(substage => substage.SubstageSnapshotId == ActiveSubstageId.Value);

        if (activeSubstage is null || activeSubstage.PlayMode != SubstagePlayMode.Trivia)
        {
            throw new ClueNotReleasableException(clueId);
        }

        return MissionRuntimeSnapshot.ClueSnapshots.SingleOrDefault(clue =>
                clue.ClueSnapshotId == clueId &&
                clue.SubstageSnapshotId == activeSubstage.SubstageSnapshotId &&
                clue.IsHiddenUntilOperatorRelease &&
                !string.IsNullOrWhiteSpace(clue.Text))
            ?? throw new ClueNotReleasableException(clueId);
    }

    private void ResolveReleasableSubject(ClueReleaseSubject subject)
    {
        if (subject.TargetId.HasValue)
        {
            ResolveReleasableTarget(subject.TargetId.Value);
            return;
        }

        ResolveReleasableSubstageClue(subject.ClueId!.Value);
    }

    private void EnsureClueNotAlreadyReleased(Guid teamId, ClueReleaseSubject subject)
    {
        if (_clueReleaseRecords.Any(record =>
                record.TeamId == teamId &&
                record.TargetId == subject.TargetId &&
                record.ClueId == subject.ClueId))
        {
            throw new ClueAlreadyReleasedToTeamException(
                teamId,
                subject.TargetId ?? subject.ClueId!.Value);
        }
    }

    private void AppendManualClueRelease(
        ClueReleaseSubject subject,
        Team team,
        int operatorUserId,
        DateTimeOffset releasedAt)
    {
        var release = ClueReleaseRecord.CreateManual(
            LiveSessionId,
            team.TeamId,
            subject,
            operatorUserId: operatorUserId,
            releasedAt: releasedAt);

        _clueReleaseRecords.Add(release);
        team.IncrementReleasedClueCount();
        AddDomainEvent(new ClueReleasedEvent(
            LiveSessionId,
            team.TeamId,
            release.TargetId,
            release.ClueId,
            release.ReleaseMode,
            release.ReleasedByUserId,
            release.ReleasedAt));
    }

    private IReadOnlyList<VisibleClue> CollectSubstageVisibleClues(Guid substageSnapshotId, Guid teamId)
    {
        // Two-group order mirrors target clues: initial clues stay pinned by SequenceOrder, then
        // operator-released hidden clues render newest-first with SequenceOrder breaking ties.
        return MissionRuntimeSnapshot.ClueSnapshots
            .Where(clue =>
                clue.SubstageSnapshotId == substageSnapshotId &&
                !string.IsNullOrWhiteSpace(clue.Text))
            .Select(clue => new
            {
                clue,
                release = _clueReleaseRecords.SingleOrDefault(record =>
                    record.TeamId == teamId &&
                    record.ClueId == clue.ClueSnapshotId),
            })
            .Where(entry => entry.clue.IsVisibleWhenSubstageStarts || entry.release is not null)
            .OrderBy(entry => entry.clue.IsVisibleWhenSubstageStarts ? 0 : 1)
            .ThenByDescending(entry =>
                entry.clue.IsVisibleWhenSubstageStarts
                    ? DateTimeOffset.MinValue
                    : entry.release!.ReleasedAt)
            .ThenBy(entry => entry.clue.SequenceOrder)
            .Select(entry => VisibleClue.CreateForSubstage(
                entry.clue.ClueSnapshotId,
                entry.clue.Text))
            .ToList();
    }

    // Surfaces every active target in the active treasure-hunt substage with its display
    // coordinates so the participant's mobile app can render the targets on a map. Coordinates are
    // context metadata only; QR validation still owns target resolution.
    private IReadOnlyList<VisibleTarget> CollectActiveTargets()
    {
        if (ActiveSubstageId is null)
        {
            return [];
        }

        return MissionRuntimeSnapshot.TargetSnapshots
            .Where(target =>
                target.SubstageSnapshotId == ActiveSubstageId.Value &&
                target.IsActive)
            .OrderBy(target => target.SequenceOrder)
            .Select(target => VisibleTarget.Create(
                target.TargetSnapshotId,
                target.Name,
                target.SequenceOrder,
                target.Latitude,
                target.Longitude))
            .ToList();
    }

    // Generic substage advancement (ADR-0005): once the active substage has ended and its 10s ranking
    // reveal has played out (D-3), walk to the next substage in strict stage->substage order. Both play
    // modes reach this through SubstageAdvanceCoordinator — a trivia substage out of questions and a
    // treasure hunt cleared by its first team (D-1) are the same event here. A next substage exists ->
    // move the pointer and raise SubstageAdvancedEvent (the coordinator activates the first question
    // for a trivia substage; a treasure hunt needs no activation, its targets are already live). The
    // mission timer is untouched here: MaximumTime is one budget for the whole mission, seeded at start
    // and ticking straight through. No next substage -> SessionCompletion: Finished is reached ONLY
    // here and in FinishOnMissionDeadline, never operator-forced. EnsureCanAdvanceSubstage
    // above only permits this method while State is Active, so the Finished branch below applies the
    // state change directly (ApplyStateChange) rather than through MoveTo/the transition policy —
    // Active/PausedLiveSessionState.CanTransitionTo deliberately no longer allow Finished, since that
    // would also open the door to an Operator-forced Active/Paused -> Finished transition.
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
            ApplyStateChange(
                SessionState.Finished,
                occurredAt,
                reason: null,
                responsibleUserId: null,
                responsibleUserExternalId: null);
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

    // D-4: MaximumTime ran out, wherever play happened to be. The mission ends immediately on the
    // ranking, in either play mode — mission expiry is not a substage end, so it gets no 10s reveal of
    // its own (the finished screen already IS the ranking; see the spec's step-3 decision 3). The
    // active substage is left pointing where it was: it did not complete, and SubstageAdvancedEvent
    // would claim it did.
    //
    // Like the Finished branch of CompleteActiveSubstageAndAdvance this applies the state change
    // directly rather than through the transition policy, for the same reason: Active/Paused ->
    // Finished stays closed to operator-facing paths.
    public void FinishOnMissionDeadline(DateTimeOffset occurredAt)
    {
        LiveSessionStateFactory.For(State).EnsureCanAdvanceSubstage(this);

        AddDomainEvent(new MissionDeadlineReachedEvent(
            LiveSessionId,
            ActiveSubstageId,
            occurredAt));

        ApplyStateChange(
            SessionState.Finished,
            occurredAt,
            reason: null,
            responsibleUserId: null,
            responsibleUserExternalId: null);
    }

    // D-3: open the substage-end ranking reveal. Both play modes land here — a treasure hunt from
    // RegisterTargetScan on first clear (D-1), a trivia substage from the coordinator when its last
    // question's reveal completes.
    //
    // Idempotent by design: two teams can clear in the same tick, and the loser of that race must not
    // restart the window or re-raise the event. D-2 depends on this — the ranking on screen is the
    // settled result and must not move while displayed. (The `xmin` token guards the same race at the
    // DB; this guards it in the aggregate. Both are needed — see the concurrency section of the spec.)
    public void BeginSubstageRankingReveal(DateTimeOffset occurredAt, TimeSpan revealDuration)
    {
        LiveSessionStateFactory.For(State).EnsureCanAdvanceSubstage(this);

        if (ActiveSubstageId is null)
        {
            throw new NoActiveSubstageException();
        }

        if (_substageRevealUntil.HasValue)
        {
            return;
        }

        var orderedSubstages = GetOrderedSubstages();
        var currentIndex = Array.FindIndex(
            orderedSubstages,
            substage => substage.SubstageSnapshotId == ActiveSubstageId.Value);
        var currentSubstage = orderedSubstages[currentIndex];

        _substageRevealUntil = occurredAt + revealDuration;

        AddDomainEvent(new SubstageRevealStartedEvent(
            LiveSessionId,
            currentSubstage.SubstageSnapshotId,
            currentSubstage.PlayMode,
            _substageRevealUntil.Value,
            isTerminal: currentIndex + 1 >= orderedSubstages.Length,
            occurredAt));
    }

    // End the reveal window. Separate from CompleteActiveSubstageAndAdvance so the advance keeps its
    // single meaning and stays callable on its own; the coordinator pairs them. Idempotent callers
    // should guard on `IsAwaitingSubstageRankingReveal` first.
    public void CompleteSubstageRankingReveal()
    {
        if (_substageRevealUntil is null)
        {
            throw new NoActiveSubstageRevealException();
        }

        _substageRevealUntil = null;
    }

    // The reveal on screen right now (D-3), rebuilt from persisted state — not a transient event — so a
    // client reconnecting mid-reveal can restore the ranking. Null when none is active. During a reveal
    // the substage pointer has not moved, so ActiveSubstageId still names the revealed substage; the play
    // mode and terminal marker come from its position in the ordered substages. Present while paused too
    // (the field only clears on committed advancement/finish), even once the wall-clock RevealUntil has
    // passed — paused sessions are excluded from worker progression, so the reveal deliberately persists.
    public ActiveSubstageRankingReveal? GetActiveSubstageRankingReveal()
    {
        if (_substageRevealUntil is not { } revealUntil || ActiveSubstageId is null)
        {
            return null;
        }

        // _substageRevealUntil is only ever set (BeginSubstageRankingReveal) with ActiveSubstageId
        // pointing at a live substage, so the pointer always resolves here — same as that method.
        var orderedSubstages = GetOrderedSubstages();
        var currentIndex = Array.FindIndex(
            orderedSubstages,
            substage => substage.SubstageSnapshotId == ActiveSubstageId.Value);

        var currentSubstage = orderedSubstages[currentIndex];
        return new ActiveSubstageRankingReveal(
            currentSubstage.SubstageSnapshotId,
            currentSubstage.PlayMode,
            revealUntil,
            IsTerminal: currentIndex + 1 >= orderedSubstages.Length,
            EmittedAt: revealUntil - SubstageRankingRevealDuration);
    }

    public void AssignOperator(int operatorUserId, DateTimeOffset occurredAt) =>
        AssignOperator(operatorUserId, assignedOperatorExternalId: null, occurredAt);

    public void AssignOperator(int operatorUserId, string? assignedOperatorExternalId, DateTimeOffset occurredAt)
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
            assignedOperatorExternalId,
            occurredAt));
    }

    internal bool HasAdvancingQuestionTimer()
    {
        return ActiveQuestionIndex.HasValue &&
            _questionTimerAdvancingSince.HasValue &&
            _questionTimerExpiredAt is null &&
            _questionTimerRemainingDuration > TimeSpan.Zero;
    }

    internal bool HasAdvancingMissionTimer()
    {
        return ActiveSubstageId.HasValue &&
            _missionTimerAdvancingSince.HasValue &&
            _missionTimerExpiredAt is null &&
            _missionTimerRemainingDuration > TimeSpan.Zero;
    }

    internal void EnterActiveSessionState(DateTimeOffset occurredAt)
    {
        StartedAt ??= occurredAt;
        PausedAt = null;

        // Entering Active starts the first substage in strict order (CONTEXT.md:48). The null check
        // guards pause->resume so resuming never rewinds the pointer to the first substage, and never
        // reseeds the mission timer — a resumed session keeps the budget it had left.
        if (ActiveSubstageId is null)
        {
            ActiveSubstageId = GetOrderedSubstages()[0].SubstageSnapshotId;
            SeedMissionTimer(occurredAt);
        }
    }

    internal void EnterActiveQuestionTimerState(DateTimeOffset occurredAt)
    {
        ResumeQuestionTimer(occurredAt);
    }

    internal void EnterActiveMissionTimerState(DateTimeOffset occurredAt)
    {
        ResumeMissionTimer(occurredAt);
    }

    internal void EnterPausedSessionState(DateTimeOffset occurredAt)
    {
        PausedAt = occurredAt;
    }

    internal void EnterPausedQuestionTimerState(DateTimeOffset occurredAt)
    {
        FreezeQuestionTimer(occurredAt);
    }

    internal void EnterPausedMissionTimerState(DateTimeOffset occurredAt)
    {
        FreezeMissionTimer(occurredAt);
    }

    internal void EnterFinishedSessionState(DateTimeOffset occurredAt)
    {
        FreezeQuestionTimer(occurredAt);
        FreezeMissionTimer(occurredAt);
        EndedAt = occurredAt;
    }

    internal void EnterCancelledSessionState(DateTimeOffset occurredAt)
    {
        FreezeQuestionTimer(occurredAt);
        FreezeMissionTimer(occurredAt);
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

    internal AuthoritativeSessionTimerSnapshot GetAdvancingMissionTimerSnapshot(DateTimeOffset observedAt)
    {
        var remaining = CalculateAdvancingMissionTimerRemaining(observedAt);
        var expired = _missionTimerExpiredAt.HasValue || remaining <= TimeSpan.Zero;

        return AuthoritativeSessionTimerSnapshot.Create(
            _missionTimerTotalDuration,
            remaining,
            isAdvancing: !expired && HasAdvancingMissionTimer(),
            observedAt,
            _missionTimerAdvancingSince,
            _missionTimerExpiredAt);
    }

    internal AuthoritativeSessionTimerSnapshot GetFrozenMissionTimerSnapshot(DateTimeOffset observedAt)
    {
        return AuthoritativeSessionTimerSnapshot.Create(
            _missionTimerTotalDuration,
            _missionTimerExpiredAt.HasValue ? TimeSpan.Zero : _missionTimerRemainingDuration,
            isAdvancing: false,
            observedAt,
            advancingSince: null,
            _missionTimerExpiredAt);
    }

    internal AuthoritativeSessionTimerSnapshot MarkAdvancingMissionTimerExpiredIfElapsed(DateTimeOffset occurredAt)
    {
        var remaining = CalculateAdvancingMissionTimerRemaining(occurredAt);
        if (remaining > TimeSpan.Zero)
        {
            return GetAdvancingMissionTimerSnapshot(occurredAt);
        }

        _missionTimerRemainingDuration = TimeSpan.Zero;
        _missionTimerAdvancingSince = null;
        _missionTimerExpiredAt ??= occurredAt;

        return GetFrozenMissionTimerSnapshot(occurredAt);
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

    // MaximumTime is the whole mission's budget, so the timer is seeded once when the session starts
    // and then ticks across every substage of both play modes. Seeding per substage would hand each
    // one the full budget.
    private void SeedMissionTimer(DateTimeOffset occurredAt)
    {
        _missionTimerTotalDuration = TimeSpan.FromMinutes(MaximumTime.Minutes);
        _missionTimerRemainingDuration = _missionTimerTotalDuration;
        _missionTimerAdvancingSince = occurredAt;
        _missionTimerExpiredAt = null;
    }

    private void ResumeMissionTimer(DateTimeOffset occurredAt)
    {
        if (ActiveSubstageId is null || _missionTimerTotalDuration <= TimeSpan.Zero)
        {
            return;
        }

        if (_missionTimerExpiredAt is not null || _missionTimerRemainingDuration <= TimeSpan.Zero)
        {
            _missionTimerRemainingDuration = TimeSpan.Zero;
            _missionTimerAdvancingSince = null;
            _missionTimerExpiredAt ??= occurredAt;
            return;
        }

        _missionTimerAdvancingSince = occurredAt;
    }

    private void FreezeMissionTimer(DateTimeOffset occurredAt)
    {
        if (_missionTimerAdvancingSince is null)
        {
            return;
        }

        _missionTimerRemainingDuration = CalculateAdvancingMissionTimerRemaining(occurredAt);
        _missionTimerAdvancingSince = null;

        if (_missionTimerRemainingDuration <= TimeSpan.Zero)
        {
            _missionTimerRemainingDuration = TimeSpan.Zero;
            _missionTimerExpiredAt ??= occurredAt;
        }
    }

    private TimeSpan CalculateAdvancingMissionTimerRemaining(DateTimeOffset observedAt)
    {
        if (_missionTimerExpiredAt.HasValue)
        {
            return TimeSpan.Zero;
        }

        if (_missionTimerAdvancingSince is null)
        {
            return _missionTimerRemainingDuration;
        }

        var elapsed = observedAt - _missionTimerAdvancingSince.Value;
        if (elapsed <= TimeSpan.Zero)
        {
            return _missionTimerRemainingDuration;
        }

        var remaining = _missionTimerRemainingDuration - elapsed;
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

    // Participation Block (#91): mark the external participant blocked after a runtime access
    // re-check denies (deactivation/membership revocation). Returns the participant only on the
    // transition (so callers persist/evict once); null if absent or already blocked. The team slot
    // is intentionally kept — a later Users re-allow recovers the participant in place on reconnect.
    public SessionParticipant? BlockExternalParticipant(Guid externalIdentityId, DateTimeOffset occurredAt)
    {
        var participant = _participants.SingleOrDefault(participant => participant.ExternalIdentityId == externalIdentityId);
        return participant is not null && participant.Block(occurredAt) ? participant : null;
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
