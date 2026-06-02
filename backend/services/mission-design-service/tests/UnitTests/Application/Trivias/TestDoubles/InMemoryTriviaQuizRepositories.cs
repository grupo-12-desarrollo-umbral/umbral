using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.UnitTests.Application.Trivias.TestDoubles;

internal sealed class InMemoryTriviaQuizRepository : ITriviaQuizRepository
{
    private readonly Dictionary<int, TriviaQuiz> _triviaQuizzes = [];
    private int _nextId = 1;

    public TriviaQuiz? LastAddedTriviaQuiz { get; private set; }

    public TriviaQuiz? LastUpdatedTriviaQuiz { get; private set; }

    public Task<TriviaQuiz?> GetByIdAsync(int triviaQuizId, CancellationToken cancellationToken)
    {
        _triviaQuizzes.TryGetValue(triviaQuizId, out var triviaQuiz);
        return Task.FromResult(triviaQuiz);
    }

    public Task AddAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        LastAddedTriviaQuiz = triviaQuiz;

        if (triviaQuiz.Id == default)
        {
            triviaQuiz.Id = _nextId++;
        }

        _triviaQuizzes[triviaQuiz.Id] = triviaQuiz;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        LastUpdatedTriviaQuiz = triviaQuiz;
        _triviaQuizzes[triviaQuiz.Id] = triviaQuiz;
        return Task.CompletedTask;
    }

    public void Seed(TriviaQuiz triviaQuiz)
    {
        if (triviaQuiz.Id == default)
        {
            triviaQuiz.Id = _nextId++;
        }

        _triviaQuizzes[triviaQuiz.Id] = triviaQuiz;
    }
}

internal sealed class InMemoryTriviaQuizReadModelRepository : ITriviaQuizReadModelRepository
{
    private readonly IReadOnlyList<TriviaQuizSummaryDto> _catalog;
    private readonly Dictionary<int, TriviaQuizDto> _details;

    public InMemoryTriviaQuizReadModelRepository(
        IReadOnlyList<TriviaQuizSummaryDto>? catalog = null,
        IReadOnlyDictionary<int, TriviaQuizDto>? details = null)
    {
        _catalog = catalog ?? Array.Empty<TriviaQuizSummaryDto>();
        _details = details?.ToDictionary(pair => pair.Key, pair => pair.Value)
            ?? new Dictionary<int, TriviaQuizDto>();
    }

    public Task<IReadOnlyList<TriviaQuizSummaryDto>> GetTriviaCatalogAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_catalog);
    }

    public Task<TriviaQuizDto?> GetTriviaDetailAsync(int triviaQuizId, CancellationToken cancellationToken)
    {
        _details.TryGetValue(triviaQuizId, out var triviaQuiz);
        return Task.FromResult(triviaQuiz);
    }
}

internal sealed class StubClock : IClock
{
    public StubClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; }
}
