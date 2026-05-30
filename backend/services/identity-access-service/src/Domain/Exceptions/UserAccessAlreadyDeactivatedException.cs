namespace umbral_backend.Domain.Exceptions;

public sealed class UserAccessAlreadyDeactivatedException : Exception
{
    public UserAccessAlreadyDeactivatedException(int userId)
        : base($"User '{userId}' is already deactivated.")
    {
    }
}
