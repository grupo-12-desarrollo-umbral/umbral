using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Queries.GetMissionById;

public sealed record GetMissionByIdQuery(int Id) : IRequest<MissionDto>;
