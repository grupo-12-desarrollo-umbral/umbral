namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionTimeLimitRequiredToPublishException : Exception
{
    public TriviaQuestionTimeLimitRequiredToPublishException(int sequenceOrder)
        : base($"Trivia question '{sequenceOrder}' must define a time limit before publication.")
    {
    }
}
