using umbral_backend.Application.Trivias.DTOs;

namespace umbral_backend.Application.Trivias.Common.Reuse;

public interface ITriviaQuizReuseCommand : IRequest<TriviaQuizDto>
{
    int Id { get; }
}
