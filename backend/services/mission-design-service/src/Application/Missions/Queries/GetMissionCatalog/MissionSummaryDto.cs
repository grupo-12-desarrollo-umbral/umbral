namespace umbral_backend.Application.Missions.Queries.GetMissionCatalog;

public sealed record MissionSummaryDto(
    int Id,
    string Name,
    string Description,
    string Difficulty,
    string Status);
