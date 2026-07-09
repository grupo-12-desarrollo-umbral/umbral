namespace umbral_backend.Application.Sessions.Common.Notifications;

public sealed record SessionTimerUpdatedNotificationDto(
    Guid LiveSessionId,
    long RemainingMilliseconds,
    bool IsPaused,
    DateTimeOffset EmittedAt,
    long TotalMilliseconds,
    bool IsExpired,
    string SessionState);
