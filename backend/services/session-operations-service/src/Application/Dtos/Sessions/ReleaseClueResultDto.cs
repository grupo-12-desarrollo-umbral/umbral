namespace umbral_backend.Application.Dtos.Sessions;

public sealed record ReleaseClueResultDto(
    Guid? TargetId,
    Guid? ClueId,
    IReadOnlyCollection<Guid> ReleasedTeamIds);
