namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionScoreValueRequiredToPublishException : DomainException
{
    public TriviaQuestionScoreValueRequiredToPublishException(int questionId)
        : base($"Trivia question '{questionId}' must define a score value before publication.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
