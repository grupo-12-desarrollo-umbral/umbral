using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Common.Authoring;
using umbral_backend.Application.Trivias.DTOs;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Common.Lifecycle;

public abstract class TriviaQuizLifecycleCommandHandler<TCommand> : IRequestHandler<TCommand, TriviaQuizDto>
    where TCommand : ITriviaQuizLifecycleCommand
{
    private readonly ITriviaQuizRepository _triviaQuizRepository;
    private readonly IClock _clock;

    protected TriviaQuizLifecycleCommandHandler(ITriviaQuizRepository triviaQuizRepository, IClock clock)
    {
        _triviaQuizRepository = triviaQuizRepository;
        _clock = clock;
    }

    public async Task<TriviaQuizDto> Handle(TCommand request, CancellationToken cancellationToken)
    {
        var triviaQuiz = await _triviaQuizRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("TriviaQuiz", request.Id);

        await EnsureTransitionAllowedAsync(triviaQuiz, cancellationToken);

        ApplyTransition(triviaQuiz, _clock.UtcNow);

        await _triviaQuizRepository.UpdateAsync(triviaQuiz, cancellationToken);

        return TriviaQuizDtoMapper.Map(triviaQuiz);
    }

    /// <summary>
    /// Cross-aggregate precondition hook, run before the domain transition. The base
    /// transition allows everything; subclasses override to reject transitions that the
    /// quiz aggregate alone cannot see (e.g. archival blocked by an active mission reference).
    /// </summary>
    protected virtual Task EnsureTransitionAllowedAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
        => Task.CompletedTask;

    protected abstract void ApplyTransition(TriviaQuiz triviaQuiz, DateTimeOffset transitionedAt);
}
