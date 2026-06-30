using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;

namespace umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;

public sealed record GetTriviaCatalogQuery : IRequest<IReadOnlyList<TriviaQuizSummaryDto>>;
