namespace umbral_backend.Domain.Exceptions;

public sealed class MissionAlreadyActiveException : Exception
{
    public MissionAlreadyActiveException()
        : base("Mission is already active.")
    {
    }
}
