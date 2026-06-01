namespace umbral_backend.Application.Missions.DTOs;

public sealed record MissionSummaryDto(
    int Id,
    string Name,
    string Description,
    string Difficulty,
    string Status);
