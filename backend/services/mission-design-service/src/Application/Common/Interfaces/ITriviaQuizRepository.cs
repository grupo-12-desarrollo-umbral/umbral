using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface ITriviaQuizRepository
{
    Task<TriviaQuiz?> GetByIdAsync(int triviaQuizId, CancellationToken cancellationToken);

    Task AddAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken);

    Task UpdateAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken);
}
