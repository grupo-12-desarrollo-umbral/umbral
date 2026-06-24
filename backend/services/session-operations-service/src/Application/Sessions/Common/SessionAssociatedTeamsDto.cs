namespace umbral_backend.Application.Sessions.Common;

public sealed record SessionAssociatedTeamsDto(
    Guid LiveSessionId,
    IReadOnlyList<AssociatedSessionTeamDto> Teams);
