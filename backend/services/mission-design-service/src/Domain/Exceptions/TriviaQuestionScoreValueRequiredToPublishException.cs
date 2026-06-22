namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionScoreValueRequiredToPublishException : DomainException
{
    public TriviaQuestionScoreValueRequiredToPublishException(int sequenceOrder)
        : base($"Trivia question '{sequenceOrder}' must define a score value before publication.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
