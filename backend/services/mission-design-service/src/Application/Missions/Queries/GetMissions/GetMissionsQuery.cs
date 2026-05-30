using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Queries.GetMissions;

public sealed record GetMissionsQuery : IRequest<IReadOnlyList<MissionSummaryDto>>;
