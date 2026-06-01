namespace umbral_backend.Domain.Exceptions;

public sealed class MissionAlreadyDeactivatedException : Exception
{
    public MissionAlreadyDeactivatedException()
        : base("Mission is already deactivated.")
    {
    }
}
