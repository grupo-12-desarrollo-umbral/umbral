namespace umbral_backend.Application.Dtos.Sessions;

public sealed record CreateSessionResultDto(
    Guid LiveSessionId,
    string SessionCode,
    string Title,
    string SessionState,
    DateTimeOffset ScheduledAt);
