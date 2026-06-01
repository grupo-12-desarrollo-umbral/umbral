using umbral_backend.Application.Trivias.DTOs;

namespace umbral_backend.Application.Common.Interfaces;

public interface ITriviaQuizReadModelRepository
{
    Task<IReadOnlyList<TriviaQuizSummaryDto>> GetTriviaCatalogAsync(CancellationToken cancellationToken);

    Task<TriviaQuizDto?> GetTriviaDetailAsync(int triviaQuizId, CancellationToken cancellationToken);
}
