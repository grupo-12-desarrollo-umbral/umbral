using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Common.Reuse;

public abstract class TriviaQuizReuseCommandHandler<TCommand> : IRequestHandler<TCommand, TriviaQuizDto>
    where TCommand : ITriviaQuizReuseCommand
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;
    private readonly IClock _clock;

    protected TriviaQuizReuseCommandHandler(ITriviaQuizRepository triviaQuizRepository, IClock clock)
    {
        _triviaQuizRepository = triviaQuizRepository;
        _clock = clock;
    }

    public async Task<TriviaQuizDto> Handle(TCommand request, CancellationToken cancellationToken)
    {
        var triviaQuiz = await _triviaQuizRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("TriviaQuiz", request.Id);

        var updatedTriviaQuiz = ApplyReuseOperation(triviaQuiz, _clock.UtcNow);

        await PersistAsync(updatedTriviaQuiz, cancellationToken);

        return TriviaQuizDtoMapper.Map(updatedTriviaQuiz);
    }

    protected Task AddAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        return _triviaQuizRepository.AddAsync(triviaQuiz, cancellationToken);
    }

    protected Task UpdateAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        return _triviaQuizRepository.UpdateAsync(triviaQuiz, cancellationToken);
    }

    protected abstract TriviaQuiz ApplyReuseOperation(TriviaQuiz triviaQuiz, DateTimeOffset requestedAt);

    protected abstract Task PersistAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken);
}
