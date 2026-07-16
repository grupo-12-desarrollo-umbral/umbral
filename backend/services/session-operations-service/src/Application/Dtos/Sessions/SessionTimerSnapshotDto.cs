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
    int? AwaitingRevealQuestionSequenceOrder = null);

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
