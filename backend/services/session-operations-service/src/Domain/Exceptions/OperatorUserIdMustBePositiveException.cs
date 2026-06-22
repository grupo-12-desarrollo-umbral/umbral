namespace umbral_backend.Domain.Exceptions;

public sealed class OperatorUserIdMustBePositiveException : DomainException
{
    public OperatorUserIdMustBePositiveException()
        : base("Assigned operator user id must be greater than zero.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
