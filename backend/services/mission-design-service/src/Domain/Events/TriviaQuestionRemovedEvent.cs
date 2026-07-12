using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class TriviaQuestionRemovedEvent : BaseEvent
{
    public TriviaQuestionRemovedEvent(TriviaQuiz triviaQuiz, TriviaQuestion triviaQuestion)
    {
        TriviaQuiz = triviaQuiz;
        TriviaQuestion = triviaQuestion;
    }

    public TriviaQuiz TriviaQuiz { get; }

    public TriviaQuestion TriviaQuestion { get; }
}
