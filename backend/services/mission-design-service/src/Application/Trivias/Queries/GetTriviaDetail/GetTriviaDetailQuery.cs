using umbral_backend.Application.Trivias.Common;
using umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;

namespace umbral_backend.Application.Trivias.Queries.GetTriviaDetail;

public sealed record GetTriviaDetailQuery(int Id) : IRequest<TriviaQuizDto>;
