namespace umbral_backend.Domain.Exceptions;

public sealed class UserAccessAlreadyActiveException : DomainException
{
    public UserAccessAlreadyActiveException(int userId)
        : base($"User '{userId}' is already active.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
