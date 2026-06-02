using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class TriviaQuestionUpdatedEvent : BaseEvent
{
    public TriviaQuestionUpdatedEvent(TriviaQuiz triviaQuiz, TriviaQuestion triviaQuestion)
    {
        TriviaQuiz = triviaQuiz;
        TriviaQuestion = triviaQuestion;
    }

    public TriviaQuiz TriviaQuiz { get; }

    public TriviaQuestion TriviaQuestion { get; }
}
