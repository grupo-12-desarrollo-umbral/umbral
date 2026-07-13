namespace umbral_backend.Application.Dtos.Sessions;

public sealed record ReleaseClueResultDto(
    Guid TargetId,
    IReadOnlyCollection<Guid> ReleasedTeamIds);
