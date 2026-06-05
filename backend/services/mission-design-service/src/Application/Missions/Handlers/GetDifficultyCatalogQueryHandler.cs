using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Missions.Handlers;

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
