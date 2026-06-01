using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Queries.GetMissionDetail;

public sealed record GetMissionDetailQuery(int Id) : IRequest<MissionDto>;
