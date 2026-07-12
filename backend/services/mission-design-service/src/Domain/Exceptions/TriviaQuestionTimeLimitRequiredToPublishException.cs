namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionTimeLimitRequiredToPublishException : DomainException
{
    public TriviaQuestionTimeLimitRequiredToPublishException(int questionId)
        : base($"Trivia question '{questionId}' must define a time limit before publication.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
