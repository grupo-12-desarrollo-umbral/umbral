namespace umbral_backend.Application.Dtos.Sessions;

public sealed record AssignOperatorToSessionResultDto(
    Guid LiveSessionId,
    int AssignedOperatorUserId);
