using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Missions.Queries.GetMissionCatalog;

[Authorize(Roles = $"{Roles.Administrator},{Roles.Operator}")]
public sealed record GetMissionCatalogQuery : IRequest<IReadOnlyList<MissionSummaryDto>>;
