namespace umbral_backend.Application.Sessions.Common;

public sealed record AssociatedSessionTeamDto(
    Guid RuntimeTeamId,
    Guid ReferenceTeamId,
    string DisplayName,
    string TeamCode,
    string JoinStatus);
