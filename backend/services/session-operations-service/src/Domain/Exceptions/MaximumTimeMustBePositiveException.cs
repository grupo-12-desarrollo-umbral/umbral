namespace umbral_backend.Domain.Exceptions;

public sealed class MaximumTimeMustBePositiveException : DomainException
{
    public MaximumTimeMustBePositiveException()
        : base("Maximum time must be positive.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
