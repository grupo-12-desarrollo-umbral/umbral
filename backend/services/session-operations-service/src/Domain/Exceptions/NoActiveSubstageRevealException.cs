namespace umbral_backend.Domain.Exceptions;

public sealed class NoActiveSubstageRevealException : DomainException
{
    public NoActiveSubstageRevealException()
        : base("A live session must have a pending substage ranking reveal before the reveal can complete.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
