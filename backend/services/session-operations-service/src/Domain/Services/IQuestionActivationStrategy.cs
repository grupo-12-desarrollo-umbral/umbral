using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Services;

public interface IQuestionActivationStrategy
{
    int? Next(LiveSession session);
}
