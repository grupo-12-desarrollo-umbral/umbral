using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;

public sealed record GetDifficultyCatalogQuery : IRequest<IReadOnlyList<DifficultyDto>>;
