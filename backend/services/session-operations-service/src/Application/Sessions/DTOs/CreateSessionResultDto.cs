namespace umbral_backend.Application.Sessions.DTOs;

public sealed record CreateSessionResultDto(
    Guid LiveSessionId,
    string SessionCode,
    string Title,
    string SessionState,
    DateTimeOffset ScheduledAt);
