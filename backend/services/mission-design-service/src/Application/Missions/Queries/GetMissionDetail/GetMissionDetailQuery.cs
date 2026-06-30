using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;

namespace umbral_backend.Application.Missions.Queries.GetMissionDetail;

public sealed record GetMissionDetailQuery(int Id) : IRequest<MissionDto>;
