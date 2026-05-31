namespace umbral_backend.Domain.Exceptions;

public sealed class DeactivatedUserAccessDeniedException : Exception
{
    public DeactivatedUserAccessDeniedException(int userId)
        : base($"User '{userId}' is deactivated and cannot access protected capabilities.")
    {
    }
}
