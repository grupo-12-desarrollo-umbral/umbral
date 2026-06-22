namespace umbral_backend.Domain.Exceptions;

public sealed class MissionAlreadyDeactivatedException : DomainException
{
    public MissionAlreadyDeactivatedException()
        : base("Mission is already deactivated.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
