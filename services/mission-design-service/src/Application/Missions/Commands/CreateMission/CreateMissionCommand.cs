using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Commands.CreateMission;

public sealed record CreateMissionCommand(
    string Name,
    string Description,
    string Difficulty,
    int MaximumTimeMinutes) : IRequest<MissionDto>;
