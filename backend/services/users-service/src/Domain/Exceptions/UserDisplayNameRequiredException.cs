namespace umbral_backend.Domain.Exceptions;

public sealed class UserDisplayNameRequiredException : DomainException
{
    public UserDisplayNameRequiredException()
        : base("User display name is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
