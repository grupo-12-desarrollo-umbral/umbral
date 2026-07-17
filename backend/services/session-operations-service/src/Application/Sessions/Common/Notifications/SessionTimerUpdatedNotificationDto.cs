namespace umbral_backend.Application.Sessions.Common.Notifications;

// RemainingMilliseconds/TotalMilliseconds carry whichever window the active substage owns (the trivia
// question window; the mission deadline itself during a treasure hunt, which has no window of its
// own). The Mission* fields ride the same tick so the two clocks can never arrive out of step, and
// are null while the session has no deadline seeded yet (pre-start) — clients ignore unknown fields,
// so adding them here leaves existing subscribers untouched.
public sealed record SessionTimerUpdatedNotificationDto(
    Guid LiveSessionId,
    long RemainingMilliseconds,
    bool IsPaused,
    DateTimeOffset EmittedAt,
    long TotalMilliseconds,
    bool IsExpired,
    string SessionState,
    long? MissionRemainingMilliseconds = null,
    long? MissionTotalMilliseconds = null);
