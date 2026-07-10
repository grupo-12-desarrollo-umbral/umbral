namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuizMustHaveAtLeastOneQuestionToPublishException : DomainException
{
    public TriviaQuizMustHaveAtLeastOneQuestionToPublishException()
        : base("Trivia quiz must contain at least one question before publication.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    // Safe to expose: the message is a static, identifier-free publication rule.
    public override string? PublicDetail => Message;
}
