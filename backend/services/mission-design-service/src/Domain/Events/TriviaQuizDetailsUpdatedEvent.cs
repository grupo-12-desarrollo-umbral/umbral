using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class TriviaQuizDetailsUpdatedEvent : BaseEvent
{
    public TriviaQuizDetailsUpdatedEvent(TriviaQuiz triviaQuiz)
    {
        TriviaQuiz = triviaQuiz;
    }

    public TriviaQuiz TriviaQuiz { get; }
}
