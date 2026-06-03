using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Commands.ArchiveTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Lifecycle;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Handlers;

public sealed class ArchiveTriviaQuizCommandHandler : TriviaQuizLifecycleCommandHandler<ArchiveTriviaQuizCommand>
{
    public ArchiveTriviaQuizCommandHandler(ITriviaQuizRepository triviaQuizRepository, IClock clock)
        : base(triviaQuizRepository, clock)
    {
    }

    protected override void ApplyTransition(TriviaQuiz triviaQuiz, DateTimeOffset transitionedAt)
    {
        triviaQuiz.Archive(transitionedAt);
    }
}
