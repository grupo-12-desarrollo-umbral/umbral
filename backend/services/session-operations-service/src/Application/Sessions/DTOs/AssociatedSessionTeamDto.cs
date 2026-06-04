namespace umbral_backend.Application.Sessions.DTOs;

public sealed record AssociatedSessionTeamDto(
    Guid RuntimeTeamId,
    Guid ReferenceTeamId,
    string DisplayName,
    string TeamCode,
    string JoinStatus);
