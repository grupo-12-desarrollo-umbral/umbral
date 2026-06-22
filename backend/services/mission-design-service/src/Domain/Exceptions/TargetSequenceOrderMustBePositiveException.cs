namespace umbral_backend.Domain.Exceptions;

public sealed class TargetSequenceOrderMustBePositiveException : DomainException
{
    public TargetSequenceOrderMustBePositiveException()
        : base("Target sequence order must be a positive number.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
