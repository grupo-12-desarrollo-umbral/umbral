namespace umbral_backend.Domain.Exceptions;

public sealed class TargetMayReferenceAtMostOneClueException : DomainException
{
    public TargetMayReferenceAtMostOneClueException()
        : base("A target may reference at most one clue.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
