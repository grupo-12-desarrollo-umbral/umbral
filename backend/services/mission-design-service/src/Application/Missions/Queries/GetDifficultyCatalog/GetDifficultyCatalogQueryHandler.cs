using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;

public sealed class GetDifficultyCatalogQueryHandler
    : IRequestHandler<GetDifficultyCatalogQuery, IReadOnlyList<DifficultyDto>>
{
    public Task<IReadOnlyList<DifficultyDto>> Handle(GetDifficultyCatalogQuery request, CancellationToken cancellationToken)
    {
        var difficulties = Difficulty.AllowedValues
            .Select(value => new DifficultyDto(value))
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<DifficultyDto>>(difficulties);
    }
}
