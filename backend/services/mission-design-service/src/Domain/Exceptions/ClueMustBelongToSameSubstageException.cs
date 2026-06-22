namespace umbral_backend.Domain.Exceptions;

public sealed class ClueMustBelongToSameSubstageException : DomainException
{
    public ClueMustBelongToSameSubstageException()
        : base("A target may only reference a clue from its own substage.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
