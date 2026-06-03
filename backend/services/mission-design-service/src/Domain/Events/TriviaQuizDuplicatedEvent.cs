using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class TriviaQuizDuplicatedEvent : BaseEvent
{
    public TriviaQuizDuplicatedEvent(TriviaQuiz sourceTriviaQuiz, TriviaQuiz duplicatedTriviaQuiz)
    {
        SourceTriviaQuiz = sourceTriviaQuiz;
        DuplicatedTriviaQuiz = duplicatedTriviaQuiz;
    }

    public TriviaQuiz SourceTriviaQuiz { get; }

    public TriviaQuiz DuplicatedTriviaQuiz { get; }
}
