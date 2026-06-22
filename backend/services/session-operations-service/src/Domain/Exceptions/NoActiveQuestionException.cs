namespace umbral_backend.Domain.Exceptions;

public sealed class NoActiveQuestionException : DomainException
{
    public NoActiveQuestionException()
        : base("A live session must have an active question before the question can be closed.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
