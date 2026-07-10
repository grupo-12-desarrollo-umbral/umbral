namespace umbral_backend.Domain.Exceptions;

public sealed class DeactivatedUserRoleAssignmentNotAllowedException : DomainException
{
    public DeactivatedUserRoleAssignmentNotAllowedException(int userId)
        : base($"User '{userId}' is deactivated and cannot receive a new role assignment.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Unprocessable;

    // Safe to expose: client-actionable and identifier-free (the interpolated user id stays in the
    // diagnostic Message only).
    public override string? PublicDetail => "The user is deactivated and cannot receive a new role assignment.";
}
