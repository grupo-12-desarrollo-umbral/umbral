namespace umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;

public sealed record AssignOperatorToSessionResultDto(
    Guid LiveSessionId,
    int AssignedOperatorUserId);
