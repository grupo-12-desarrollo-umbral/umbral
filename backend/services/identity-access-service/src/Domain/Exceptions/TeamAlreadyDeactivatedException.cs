namespace umbral_backend.Domain.Exceptions;

public sealed class TeamAlreadyDeactivatedException : DomainException
{
    public TeamAlreadyDeactivatedException(Guid teamId)
        : base($"Team '{teamId}' is already deactivated.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
