using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;

public sealed record GetMissionRuntimePlanQuery(int Id) : IRequest<MissionRuntimePlanDto>;
