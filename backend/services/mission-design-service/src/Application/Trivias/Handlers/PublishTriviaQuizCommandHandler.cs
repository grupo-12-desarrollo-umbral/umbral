using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Trivias.Commands.PublishTriviaQuiz;
using umbral_backend.Application.Trivias.Common.Lifecycle;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Trivias.Handlers;

public sealed class PublishTriviaQuizCommandHandler : TriviaQuizLifecycleCommandHandler<PublishTriviaQuizCommand>
{
    public PublishTriviaQuizCommandHandler(ITriviaQuizRepository triviaQuizRepository, IClock clock)
        : base(triviaQuizRepository, clock)
    {
    }

    protected override void ApplyTransition(TriviaQuiz triviaQuiz, DateTimeOffset transitionedAt)
    {
        triviaQuiz.Publish(transitionedAt);
    }
}
