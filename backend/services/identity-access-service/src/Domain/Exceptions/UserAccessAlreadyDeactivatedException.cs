namespace umbral_backend.Domain.Exceptions;

public sealed class UserAccessAlreadyDeactivatedException : DomainException
{
    public UserAccessAlreadyDeactivatedException(int userId)
        : base($"User '{userId}' is already deactivated.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
