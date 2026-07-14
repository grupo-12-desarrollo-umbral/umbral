namespace umbral_backend.Application.Dtos.Sessions;

public sealed record AddOperativeClueResultDto(
    IReadOnlyList<Guid> OperativeClueIds,
    IReadOnlyList<Guid> AssignedTeamIds,
    string ClueText);
