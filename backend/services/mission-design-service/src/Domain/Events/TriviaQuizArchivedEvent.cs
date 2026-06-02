using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class TriviaQuizArchivedEvent : BaseEvent
{
    public TriviaQuizArchivedEvent(TriviaQuiz triviaQuiz)
    {
        TriviaQuiz = triviaQuiz;
    }

    public TriviaQuiz TriviaQuiz { get; }
}
