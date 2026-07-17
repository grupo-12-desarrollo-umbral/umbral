using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;
using umbral_backend.Domain.Constants;

namespace umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;

[Authorize(Roles = $"{Roles.Administrator},{Roles.Operator}")]
public sealed record GetTriviaCatalogQuery : IRequest<IReadOnlyList<TriviaQuizSummaryDto>>;
