namespace umbral_backend.Domain.Exceptions;

public sealed class SessionCodeFormatInvalidException : DomainException
{
    public SessionCodeFormatInvalidException(int requiredLength)
        : base($"Session code must be exactly {requiredLength} alphanumeric characters.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
