namespace umbral_backend.Application.Dtos.Sessions;

public sealed record AssociatedSessionTeamDto(
    Guid RuntimeTeamId,
    Guid ReferenceTeamId,
    string DisplayName,
    string TeamCode,
    string JoinStatus);
