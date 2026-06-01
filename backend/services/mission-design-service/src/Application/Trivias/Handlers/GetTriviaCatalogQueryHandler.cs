using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;

namespace umbral_backend.Application.Trivias.Handlers;

public sealed class GetTriviaCatalogQueryHandler : IRequestHandler<GetTriviaCatalogQuery, IReadOnlyList<TriviaQuizSummaryDto>>
{
    private readonly ITriviaQuizReadModelRepository _triviaQuizReadModelRepository;

    public GetTriviaCatalogQueryHandler(ITriviaQuizReadModelRepository triviaQuizReadModelRepository)
    {
        _triviaQuizReadModelRepository = triviaQuizReadModelRepository;
    }

    public async Task<IReadOnlyList<TriviaQuizSummaryDto>> Handle(GetTriviaCatalogQuery request, CancellationToken cancellationToken)
    {
        return await _triviaQuizReadModelRepository.GetTriviaCatalogAsync(cancellationToken);
    }
}
