namespace umbral_backend.Application.Sessions.DTOs;

public sealed record TransitionSessionStateResultDto(
    Guid LiveSessionId,
    string PreviousState,
    string CurrentState,
    DateTimeOffset TransitionedAt,
    SessionTimerSnapshotDto? Timer = null);
