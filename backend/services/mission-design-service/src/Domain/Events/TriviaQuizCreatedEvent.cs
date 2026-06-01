using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class TriviaQuizCreatedEvent : BaseEvent
{
    public TriviaQuizCreatedEvent(TriviaQuiz triviaQuiz)
    {
        TriviaQuiz = triviaQuiz;
    }

    public TriviaQuiz TriviaQuiz { get; }
}
