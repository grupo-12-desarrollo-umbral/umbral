namespace umbral_backend.Application.Sessions.DTOs;

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
    ActiveQuestionSnapshotDto? ActiveQuestion = null);

public sealed record ActiveQuestionSnapshotDto(
    Guid LiveSessionId,
    int QuestionIndex,
    int SequenceOrder,
    string Prompt,
    IReadOnlyList<string> Options,
    int TimeLimitSeconds,
    int RemainingSeconds,
    DateTimeOffset ActivatedAt);
