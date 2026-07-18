using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Common.Interfaces;

public interface ITriviaQuizRepository
{
    Task<TriviaQuiz?> GetByIdAsync(int triviaQuizId, CancellationToken cancellationToken);

    /// <summary>
    /// Batched status lookup for cross-aggregate publication checks. Returns only the
    /// quizzes that exist; callers treat a missing id as "not published".
    /// </summary>
    Task<IReadOnlyDictionary<int, TriviaQuizStatus>> GetStatusesByIdsAsync(
        IReadOnlyCollection<int> triviaQuizIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Batched sum of active question timer seconds per quiz, for the trivia time-budget readiness
    /// check. Returns only quizzes that exist; a missing id is treated as contributing no time.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetActiveQuestionTimerSecondsByIdsAsync(
        IReadOnlyCollection<int> triviaQuizIds,
        CancellationToken cancellationToken);

    Task AddAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken);

    Task UpdateAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken);

    Task RemoveAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Trivia quiz removal must be implemented by the persistence layer.");
    }
}
