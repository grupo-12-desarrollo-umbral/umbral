namespace umbral_backend.Domain.Exceptions;

public sealed class ClueReleaseSubjectInvalidException : DomainException
{
    public ClueReleaseSubjectInvalidException()
        : base("A clue release must identify exactly one non-empty target or substage clue.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
