namespace umbral_backend.Application.Sessions.Common.Notifications;

// RemainingMilliseconds/TotalMilliseconds carry whichever window the active substage owns (the trivia
// question window; the mission deadline itself during a treasure hunt, which has no window of its
// own). The Mission* fields ride the same tick so the two clocks can never arrive out of step, and
// are null while the session has no deadline seeded yet (pre-start) — clients ignore unknown fields,
// so adding them here leaves existing subscribers untouched.
//
// IsPregameCountdown explicitly distinguishes the trivia pre-game countdown ticks (a 5s pre-round
// numeral) from a real question/deadline window, because the two are otherwise indistinguishable by
// duration alone: authoring permits question limits as short as 5s, exactly the pre-game total. True
// on countdown ticks, false on window ticks; a null (older replica mid-deploy that omits the field)
// tells clients to fall back to the legacy short-window heuristic.
public sealed record SessionTimerUpdatedNotificationDto(
    Guid LiveSessionId,
    long RemainingMilliseconds,
    bool IsPaused,
    DateTimeOffset EmittedAt,
    long TotalMilliseconds,
    bool IsExpired,
    string SessionState,
    long? MissionRemainingMilliseconds = null,
    long? MissionTotalMilliseconds = null,
    bool? IsPregameCountdown = null);
