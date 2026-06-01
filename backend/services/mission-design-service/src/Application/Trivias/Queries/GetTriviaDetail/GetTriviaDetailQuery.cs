using umbral_backend.Application.Trivias.DTOs;

namespace umbral_backend.Application.Trivias.Queries.GetTriviaDetail;

public sealed record GetTriviaDetailQuery(int Id) : IRequest<TriviaQuizDto>;
