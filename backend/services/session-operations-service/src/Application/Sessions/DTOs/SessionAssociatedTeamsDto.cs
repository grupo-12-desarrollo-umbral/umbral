namespace umbral_backend.Application.Sessions.DTOs;

public sealed record SessionAssociatedTeamsDto(
    Guid LiveSessionId,
    IReadOnlyList<AssociatedSessionTeamDto> Teams);
