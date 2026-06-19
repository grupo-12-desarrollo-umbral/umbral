namespace umbral_backend.Domain.Exceptions;

public sealed class MissionNodeTitleRequiredException : Exception
{
    public MissionNodeTitleRequiredException()
        : base("Mission node title is required.")
    {
    }
}
