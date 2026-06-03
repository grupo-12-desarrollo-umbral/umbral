using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Common.Interfaces;

public interface IPublishedTriviaQuizSource
{
    Task<PublishedTriviaQuizDto?> GetByIdAsync(int triviaQuizId, CancellationToken cancellationToken);
}
