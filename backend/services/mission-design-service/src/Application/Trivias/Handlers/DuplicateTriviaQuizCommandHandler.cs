using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Commands.DuplicateTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Reuse;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Handlers;

public sealed class DuplicateTriviaQuizCommandHandler : TriviaQuizReuseCommandHandler<DuplicateTriviaQuizCommand>
{
    public DuplicateTriviaQuizCommandHandler(ITriviaQuizRepository triviaQuizRepository, IClock clock)
        : base(triviaQuizRepository, clock)
    {
    }

    protected override TriviaQuiz ApplyReuseOperation(TriviaQuiz triviaQuiz, DateTimeOffset requestedAt)
    {
        return triviaQuiz.Duplicate();
    }

    protected override Task PersistAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        return AddAsync(triviaQuiz, cancellationToken);
    }
}
