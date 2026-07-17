namespace umbral_backend.Application.Dtos.Sessions;

public sealed record SessionTimerSnapshotDto(
    Guid LiveSessionId,
    Guid? TeamId,
    string SessionState,
    int TotalSeconds,
    int RemainingSeconds,
    string TimerStatus,
    bool IsAdvancing,
    bool IsExpired,
    DateTimeOffset ObservedAt,
    DateTimeOffset? AdvancingSince,
    DateTimeOffset? ExpiredAt,
    ActiveQuestionSnapshotDto? ActiveQuestion = null,
    // The sequence order of the just-closed trivia question during the HU-35 reveal window (when
    // ActiveQuestion is null but a result is being shown), else null. Lets an operator who opens the
    // session mid-reveal — with no prior QuestionActivated to key off — fetch that question's answer
    // review (HU-36B AC4). Only a sequence order: reveals no option/correctness a pre-close read couldn't.
    int? AwaitingRevealQuestionSequenceOrder = null,
    // The mission deadline (D-4), carried alongside the active-substage window above so a client
    // reconnecting mid-question can restore the whole-mission clock — not just the trivia question
    // window, which is all Total/RemainingSeconds carry during a trivia substage. Mirrors the
    // MissionTotal/RemainingMilliseconds on the live SessionTimerUpdated event. Null until the deadline
    // is seeded at session start; during a treasure hunt these equal Total/RemainingSeconds (that
    // substage has no window of its own and already displays the deadline).
    int? MissionTotalSeconds = null,
    int? MissionRemainingSeconds = null,
    // The substage ranking reveal on screen right now (D-3), or null when none is active. The
    // SubstageRankingRevealStarted push opens the reveal with low latency; this snapshot field is the
    // recovery source, so a participant reconnecting mid-reveal restores the same ranking screen rather
    // than dropping back to gameplay. Present while paused and past the wall-clock RevealUntil — it
    // clears only when the backend commits advancement or finish.
    ActiveRankingRevealSnapshotDto? ActiveRankingReveal = null);

public sealed record ActiveRankingRevealSnapshotDto(
    Guid SubstageSnapshotId,
    string PlayMode,
    DateTimeOffset RevealUntil,
    bool IsTerminal,
    DateTimeOffset EmittedAt);

public sealed record ActiveQuestionSnapshotDto(
    Guid LiveSessionId,
    int QuestionIndex,
    int SequenceOrder,
    string Prompt,
    IReadOnlyList<string> Options,
    int TimeLimitSeconds,
    int RemainingSeconds,
    DateTimeOffset ActivatedAt,
    Guid TriviaSubstageSnapshotId);
