namespace umbral_backend.Domain.Exceptions;

public sealed class NoActiveQuestionRevealException : DomainException
{
    public NoActiveQuestionRevealException()
        : base("A live session must have a pending question reveal before the reveal can complete.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
