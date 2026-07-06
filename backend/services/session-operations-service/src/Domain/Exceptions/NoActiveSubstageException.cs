namespace umbral_backend.Domain.Exceptions;

public sealed class NoActiveSubstageException : DomainException
{
    public NoActiveSubstageException()
        : base("A live session must have an active substage before it can advance to the next substage.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
