namespace umbral_backend.Application.Sessions.DTOs;

public sealed record AssignOperatorToSessionResultDto(
    Guid LiveSessionId,
    int AssignedOperatorUserId);
