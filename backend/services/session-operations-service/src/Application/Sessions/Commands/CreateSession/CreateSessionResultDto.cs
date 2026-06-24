namespace umbral_backend.Application.Sessions.Commands.CreateSession;

public sealed record CreateSessionResultDto(
    Guid LiveSessionId,
    string SessionCode,
    string Title,
    string SessionState,
    DateTimeOffset ScheduledAt);
