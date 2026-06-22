namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionTimeLimitRequiredToPublishException : DomainException
{
    public TriviaQuestionTimeLimitRequiredToPublishException(int sequenceOrder)
        : base($"Trivia question '{sequenceOrder}' must define a time limit before publication.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
