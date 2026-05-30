namespace umbral_backend.Application.Missions.DTOs;

public sealed record MissionDto(
    int Id,
    string Name,
    string Description,
    string Difficulty,
    int MaximumTimeMinutes,
    string Status);
