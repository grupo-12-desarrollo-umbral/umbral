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

    private readonly List<Team> _teams = new();
    private readonly List<SessionParticipant> _participants = new();
    private readonly List<JoinContext> _joinContexts = new();
    private readonly List<TriviaAnswerSubmission> _triviaAnswerSubmissions = new();
    private readonly List<SessionEvent> _sessionEvents = new();
    private readonly List<ClueReleaseRecord> _clueReleaseRecords = new();
    private TimeSpan _questionTimerTotalDuration;
    private TimeSpan _questionTimerRemainingDuration;
    private DateTimeOffset? _questionTimerAdvancingSince;
    private DateTimeOffset? _questionTimerExpiredAt;
    private TimeSpan _substageTimerTotalDuration;
    private TimeSpan _substageTimerRemainingDuration;
    private DateTimeOffset? _substageTimerAdvancingSince;
    private DateTimeOffset? _substageTimerExpiredAt;

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
        _substageTimerTotalDuration = TimeSpan.Zero;
        _substageTimerRemainingDuration = TimeSpan.Zero;
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
        _substageTimerTotalDuration = TimeSpan.Zero;
        _substageTimerRemainingDuration = TimeSpan.Zero;
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

    public bool IsSubstageTimerAdvancing => LiveSessionStateFactory.For(State).IsSubstageTimerAdvancing(this);

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

    public void MoveTo(
        SessionState nextState,
        DateTimeOffset occurredAt,
        SessionStateTransitionPolicy transitionPolicy,
        string? reason = null,
        int? responsibleUserId = null)
    {
        ArgumentNullException.ThrowIfNull(transitionPolicy);

        transitionPolicy.EnsureCanTransition(State, nextState, AssociatedTeamCount);

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
            actorType));
    }

    // Authoritative displayed remaining time is selected by the active substage's play mode:
    // TreasureHunt owns a substage window; Trivia keeps the active-question window.
    public AuthoritativeSessionTimerSnapshot GetAuthoritativeSessionTimerSnapshot(DateTimeOffset observedAt)
    {
        if (ActiveSubstageId is null)
        {
            return GetActiveQuestionTimerSnapshot(observedAt);
        }

        var activeSubstage = GetOrderedSubstages()
            .SingleOrDefault(substage => substage.SubstageSnapshotId == ActiveSubstageId.Value);

        return activeSubstage?.PlayMode == SubstagePlayMode.TreasureHunt
            ? LiveSessionStateFactory.For(State).GetSubstageTimerSnapshot(this, observedAt)
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

    public AuthoritativeSessionTimerSnapshot MarkQuestionTimerExpiredIfElapsed(DateTimeOffset occurredAt)
    {
        return LiveSessionStateFactory.For(State).MarkQuestionTimerExpiredIfElapsed(this, occurredAt);
    }

    public AuthoritativeSessionTimerSnapshot MarkSubstageTimerExpiredIfElapsed(DateTimeOffset occurredAt)
    {
        return LiveSessionStateFactory.For(State).MarkSubstageTimerExpiredIfElapsed(this, occurredAt);
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

    public void ReleaseClue(Guid targetId, Guid teamId, int operatorUserId, DateTimeOffset now)
    {
        EnsureSessionActiveForClueRelease();
        EnsureOperatorUserIdIsValid(operatorUserId);
        var target = ResolveReleasableTarget(targetId);
        var team = GetTeam(teamId);
        EnsureClueNotAlreadyReleased(team.TeamId, target.TargetSnapshotId);
        AppendManualClueRelease(target, team, operatorUserId, now);
    }

    public void ReleaseClueToAllTeams(Guid targetId, int operatorUserId, DateTimeOffset now)
    {
        EnsureSessionActiveForClueRelease();
        EnsureOperatorUserIdIsValid(operatorUserId);
        var target = ResolveReleasableTarget(targetId);

        foreach (var team in _teams)
        {
            EnsureClueNotAlreadyReleased(team.TeamId, target.TargetSnapshotId);
        }

        foreach (var team in _teams)
        {
            AppendManualClueRelease(target, team, operatorUserId, now);
        }
    }

    public IReadOnlyCollection<ClueReleaseRecord> GetClueReleaseRecords()
    {
        return _clueReleaseRecords.AsReadOnly();
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

            submission.AcceptRegisteredAnswer();
            _triviaAnswerSubmissions.Add(submission);
            RaiseAnswerRegistered(submission);
            return submission;
        }
        catch (EvidenceSubmissionContextRequiredException) when (ActiveSubstageId is null)
        {
            throw new TriviaAnswerRequiresTriviaSubstageException();
        }
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
            EvidenceValidationState.Pending));

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
        AddDomainEvent(new AnswerRegisteredEvent(
            LiveSessionId,
            submission.TeamId,
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
        var activeSubstageContext = BuildActiveSubstageContext();
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
        var activeSubstageContext = BuildActiveSubstageContext();

        var teamProgress = _teams
            .OrderBy(team => team.TeamCode.Value, StringComparer.Ordinal)
            .Select(team => OperatorTeamProgress.Create(
                team.TeamId,
                team.TeamCode.Value,
                team.DisplayName,
                team.CurrentScore ?? 0,
                activeSubstageContext))
            .ToList();

        return OperatorSessionPanelSnapshot.Create(
            LiveSessionId,
            State,
            timerSnapshot,
            teamProgress);
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

    private ActiveSubstageContext? BuildActiveSubstageContext()
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
            ? BuildTreasureHuntContext(activeSubstage)
            : BuildTriviaContext(activeSubstage);
    }

    private ActiveSubstageContext BuildTreasureHuntContext(SubstageSnapshot substage)
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

        // Target resolution persistence does not exist yet (HU-31 owns it). Report 0 resolved
        // and expose total active targets. Do NOT infer from CurrentClueNodeId or ReleasedClueCount.
        var resolvedTargets = 0;

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

        // Treasure-hunt clues resolve per-target (unchanged). A trivia substage has no targets, so
        // its clues resolve from the substage-scoped clue snapshot instead (#145).
        return activeSubstage.PlayMode == SubstagePlayMode.TreasureHunt
            ? CollectTargetVisibleClues(activeSubstage.SubstageSnapshotId, teamId)
            : CollectSubstageVisibleClues(activeSubstage.SubstageSnapshotId);
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

    private void EnsureClueNotAlreadyReleased(Guid teamId, Guid targetId)
    {
        if (_clueReleaseRecords.Any(record => record.TeamId == teamId && record.TargetId == targetId))
        {
            throw new ClueAlreadyReleasedToTeamException(teamId, targetId);
        }
    }

    private void AppendManualClueRelease(
        TargetSnapshot target,
        Team team,
        int operatorUserId,
        DateTimeOffset releasedAt)
    {
        // TargetSnapshot embeds its clue fields and carries no distinct clue-node identity.
        var release = ClueReleaseRecord.CreateManual(
            LiveSessionId,
            team.TeamId,
            target.TargetSnapshotId,
            clueId: null,
            operatorUserId: operatorUserId,
            releasedAt: releasedAt);

        _clueReleaseRecords.Add(release);
        team.IncrementReleasedClueCount();
        AddDomainEvent(new ClueReleasedEvent(
            LiveSessionId,
            team.TeamId,
            target.TargetSnapshotId,
            release.ClueId,
            release.ReleaseMode,
            release.ReleasedByUserId,
            release.ReleasedAt));
    }

    private IReadOnlyList<VisibleClue> CollectSubstageVisibleClues(Guid substageSnapshotId)
    {
        // Honor ClueVisibilityPolicy for a target-less substage (#145): VisibleWhenSubstageStarts
        // clues surface as soon as the substage is active; HiddenUntilOperatorRelease clues stay
        // withheld until an operator release exists (HU-26/HU-28), so they are not projected here.
        return MissionRuntimeSnapshot.ClueSnapshots
            .Where(clue =>
                clue.SubstageSnapshotId == substageSnapshotId &&
                clue.IsVisibleWhenSubstageStarts &&
                !string.IsNullOrWhiteSpace(clue.Text))
            .OrderBy(clue => clue.SequenceOrder)
            .Select(clue => VisibleClue.CreateForSubstage(clue.Text))
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
        SeedSubstageTimerIfTreasureHunt(nextSubstage, occurredAt);
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

    internal bool HasAdvancingSubstageTimer()
    {
        return ActiveSubstageId.HasValue &&
            _substageTimerAdvancingSince.HasValue &&
            _substageTimerExpiredAt is null &&
            _substageTimerRemainingDuration > TimeSpan.Zero;
    }

    internal void EnterActiveSessionState(DateTimeOffset occurredAt)
    {
        StartedAt ??= occurredAt;
        PausedAt = null;

        // Entering Active starts the first substage in strict order (CONTEXT.md:48). `??=` guards
        // pause->resume so resuming never rewinds the pointer to the first substage.
        if (ActiveSubstageId is null)
        {
            var firstSubstage = GetOrderedSubstages()[0];
            ActiveSubstageId = firstSubstage.SubstageSnapshotId;
            SeedSubstageTimerIfTreasureHunt(firstSubstage, occurredAt);
        }
    }

    internal void EnterActiveQuestionTimerState(DateTimeOffset occurredAt)
    {
        ResumeQuestionTimer(occurredAt);
    }

    internal void EnterActiveSubstageTimerState(DateTimeOffset occurredAt)
    {
        ResumeSubstageTimer(occurredAt);
    }

    internal void EnterPausedSessionState(DateTimeOffset occurredAt)
    {
        PausedAt = occurredAt;
    }

    internal void EnterPausedQuestionTimerState(DateTimeOffset occurredAt)
    {
        FreezeQuestionTimer(occurredAt);
    }

    internal void EnterPausedSubstageTimerState(DateTimeOffset occurredAt)
    {
        FreezeSubstageTimer(occurredAt);
    }

    internal void EnterFinishedSessionState(DateTimeOffset occurredAt)
    {
        FreezeQuestionTimer(occurredAt);
        FreezeSubstageTimer(occurredAt);
        EndedAt = occurredAt;
    }

    internal void EnterCancelledSessionState(DateTimeOffset occurredAt)
    {
        FreezeQuestionTimer(occurredAt);
        FreezeSubstageTimer(occurredAt);
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

    internal AuthoritativeSessionTimerSnapshot GetAdvancingSubstageTimerSnapshot(DateTimeOffset observedAt)
    {
        var remaining = CalculateAdvancingSubstageTimerRemaining(observedAt);
        var expired = _substageTimerExpiredAt.HasValue || remaining <= TimeSpan.Zero;

        return AuthoritativeSessionTimerSnapshot.Create(
            _substageTimerTotalDuration,
            remaining,
            isAdvancing: !expired && HasAdvancingSubstageTimer(),
            observedAt,
            _substageTimerAdvancingSince,
            _substageTimerExpiredAt);
    }

    internal AuthoritativeSessionTimerSnapshot GetFrozenSubstageTimerSnapshot(DateTimeOffset observedAt)
    {
        return AuthoritativeSessionTimerSnapshot.Create(
            _substageTimerTotalDuration,
            _substageTimerExpiredAt.HasValue ? TimeSpan.Zero : _substageTimerRemainingDuration,
            isAdvancing: false,
            observedAt,
            advancingSince: null,
            _substageTimerExpiredAt);
    }

    internal AuthoritativeSessionTimerSnapshot MarkAdvancingSubstageTimerExpiredIfElapsed(DateTimeOffset occurredAt)
    {
        var remaining = CalculateAdvancingSubstageTimerRemaining(occurredAt);
        if (remaining > TimeSpan.Zero)
        {
            return GetAdvancingSubstageTimerSnapshot(occurredAt);
        }

        _substageTimerRemainingDuration = TimeSpan.Zero;
        _substageTimerAdvancingSince = null;
        _substageTimerExpiredAt ??= occurredAt;

        return GetFrozenSubstageTimerSnapshot(occurredAt);
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

    private void SeedSubstageTimerIfTreasureHunt(SubstageSnapshot substage, DateTimeOffset occurredAt)
    {
        if (substage.PlayMode != SubstagePlayMode.TreasureHunt)
        {
            return;
        }

        _substageTimerTotalDuration = TimeSpan.FromMinutes(MaximumTime.Minutes);
        _substageTimerRemainingDuration = _substageTimerTotalDuration;
        _substageTimerAdvancingSince = occurredAt;
        _substageTimerExpiredAt = null;
    }

    private void ResumeSubstageTimer(DateTimeOffset occurredAt)
    {
        if (ActiveSubstageId is null || _substageTimerTotalDuration <= TimeSpan.Zero)
        {
            return;
        }

        if (_substageTimerExpiredAt is not null || _substageTimerRemainingDuration <= TimeSpan.Zero)
        {
            _substageTimerRemainingDuration = TimeSpan.Zero;
            _substageTimerAdvancingSince = null;
            _substageTimerExpiredAt ??= occurredAt;
            return;
        }

        _substageTimerAdvancingSince = occurredAt;
    }

    private void FreezeSubstageTimer(DateTimeOffset occurredAt)
    {
        if (_substageTimerAdvancingSince is null)
        {
            return;
        }

        _substageTimerRemainingDuration = CalculateAdvancingSubstageTimerRemaining(occurredAt);
        _substageTimerAdvancingSince = null;

        if (_substageTimerRemainingDuration <= TimeSpan.Zero)
        {
            _substageTimerRemainingDuration = TimeSpan.Zero;
            _substageTimerExpiredAt ??= occurredAt;
        }
    }

    private TimeSpan CalculateAdvancingSubstageTimerRemaining(DateTimeOffset observedAt)
    {
        if (_substageTimerExpiredAt.HasValue)
        {
            return TimeSpan.Zero;
        }

        if (_substageTimerAdvancingSince is null)
        {
            return _substageTimerRemainingDuration;
        }

        var elapsed = observedAt - _substageTimerAdvancingSince.Value;
        if (elapsed <= TimeSpan.Zero)
        {
            return _substageTimerRemainingDuration;
        }

        var remaining = _substageTimerRemainingDuration - elapsed;
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
