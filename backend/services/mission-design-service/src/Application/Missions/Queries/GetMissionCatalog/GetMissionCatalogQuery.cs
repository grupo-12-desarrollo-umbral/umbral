using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Queries.GetMissionCatalog;

public sealed record GetMissionCatalogQuery : IRequest<IReadOnlyList<MissionSummaryDto>>;
