namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionScoreValueRequiredToPublishException : Exception
{
    public TriviaQuestionScoreValueRequiredToPublishException(int sequenceOrder)
        : base($"Trivia question '{sequenceOrder}' must define a score value before publication.")
    {
    }
}
