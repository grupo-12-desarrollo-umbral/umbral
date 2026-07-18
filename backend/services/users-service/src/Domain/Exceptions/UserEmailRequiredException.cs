namespace umbral_backend.Domain.Exceptions;

public sealed class UserEmailRequiredException : DomainException
{
    public UserEmailRequiredException()
        : base("User email is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
