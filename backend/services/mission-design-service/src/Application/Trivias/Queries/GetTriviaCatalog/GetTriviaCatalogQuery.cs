using umbral_backend.Application.Trivias.DTOs;

namespace umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;

public sealed record GetTriviaCatalogQuery : IRequest<IReadOnlyList<TriviaQuizSummaryDto>>;
