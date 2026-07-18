namespace umbral_backend.Domain.Exceptions;

public sealed class MaximumTimeExceedsLimitException : DomainException
{
    public MaximumTimeExceedsLimitException(int limitMinutes)
        : base($"Mission maximum time must not exceed {limitMinutes} minutes.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
