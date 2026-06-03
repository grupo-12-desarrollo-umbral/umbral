using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class TriviaQuizPublishedEvent : BaseEvent
{
    public TriviaQuizPublishedEvent(TriviaQuiz triviaQuiz)
    {
        TriviaQuiz = triviaQuiz;
    }

    public TriviaQuiz TriviaQuiz { get; }
}
