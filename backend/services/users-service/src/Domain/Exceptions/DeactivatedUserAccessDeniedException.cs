namespace umbral_backend.Domain.Exceptions;

public sealed class DeactivatedUserAccessDeniedException : DomainException
{
    public DeactivatedUserAccessDeniedException(int userId)
        : base($"User '{userId}' is deactivated and cannot access protected capabilities.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Forbidden;
}
