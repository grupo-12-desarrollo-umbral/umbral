namespace umbral_backend.Domain.Exceptions;

public sealed class MissionNameRequiredException : Exception
{
    public MissionNameRequiredException()
        : base("Mission name is required.")
    {
    }
}
