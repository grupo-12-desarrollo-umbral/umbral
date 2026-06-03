using umbral_backend.Application.Trivias.DTOs;

namespace umbral_backend.Application.Trivias.Common.Lifecycle;

public interface ITriviaQuizLifecycleCommand : IRequest<TriviaQuizDto>
{
    int Id { get; }
}
