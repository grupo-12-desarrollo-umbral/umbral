namespace umbral_backend.Domain.Exceptions;

public sealed class MissionAlreadyActiveException : DomainException
{
    public MissionAlreadyActiveException()
        : base("Mission is already active.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
