namespace umbral_backend.Application.Dtos.Sessions;

public sealed record SessionAssociatedTeamsDto(
    Guid LiveSessionId,
    IReadOnlyList<AssociatedSessionTeamDto> Teams);
