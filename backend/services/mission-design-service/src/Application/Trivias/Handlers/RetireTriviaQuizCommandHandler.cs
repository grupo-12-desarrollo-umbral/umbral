using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Commands.RetireTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Reuse;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Handlers;

public sealed class RetireTriviaQuizCommandHandler : TriviaQuizReuseCommandHandler<RetireTriviaQuizCommand>
{
    public RetireTriviaQuizCommandHandler(ITriviaQuizRepository triviaQuizRepository, IClock clock)
        : base(triviaQuizRepository, clock)
    {
    }

    protected override TriviaQuiz ApplyReuseOperation(TriviaQuiz triviaQuiz, DateTimeOffset requestedAt)
    {
        triviaQuiz.RetireFromFutureUse(requestedAt);
        return triviaQuiz;
    }

    protected override Task PersistAsync(TriviaQuiz triviaQuiz, CancellationToken cancellationToken)
    {
        return UpdateAsync(triviaQuiz, cancellationToken);
    }
}
