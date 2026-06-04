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
    DateTimeOffset? ExpiredAt);
