namespace umbral_backend.Domain.Exceptions;

public sealed class MissionDescriptionRequiredException : Exception
{
    public MissionDescriptionRequiredException()
        : base("Mission description is required.")
    {
    }
}
