using umbral_backend.Application.Sessions.Common;
namespace umbral_backend.Application.Sessions.Commands.TransitionSessionState;

public sealed record TransitionSessionStateResultDto(
    Guid LiveSessionId,
    string PreviousState,
    string CurrentState,
    DateTimeOffset TransitionedAt,
    SessionTimerSnapshotDto? Timer = null);
