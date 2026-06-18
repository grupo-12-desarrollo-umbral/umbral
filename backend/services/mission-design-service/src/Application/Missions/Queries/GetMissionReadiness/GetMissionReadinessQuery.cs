using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Queries.GetMissionReadiness;

public sealed record GetMissionReadinessQuery(int Id) : IRequest<MissionReadinessDto>;
