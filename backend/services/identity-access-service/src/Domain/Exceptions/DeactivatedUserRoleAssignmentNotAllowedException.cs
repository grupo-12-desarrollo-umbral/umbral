namespace umbral_backend.Domain.Exceptions;

public sealed class DeactivatedUserRoleAssignmentNotAllowedException : DomainException
{
    public DeactivatedUserRoleAssignmentNotAllowedException(int userId)
        : base($"User '{userId}' is deactivated and cannot receive a new role assignment.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Unprocessable;
}
