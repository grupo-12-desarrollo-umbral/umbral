namespace umbral_backend.Application.Dtos.Missions;

public sealed record MissionSummaryDto(
    int Id,
    string Name,
    string Description,
    string Difficulty,
    string Status);
