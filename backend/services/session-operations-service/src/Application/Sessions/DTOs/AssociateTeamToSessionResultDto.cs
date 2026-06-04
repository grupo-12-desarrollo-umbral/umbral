namespace umbral_backend.Application.Sessions.DTOs;

public sealed record AssociateTeamToSessionResultDto(
    Guid LiveSessionId,
    Guid RuntimeTeamId,
    Guid ReferenceTeamId,
    string DisplayName,
    string TeamCode,
    string SessionState,
    int AssociatedTeamCount);
