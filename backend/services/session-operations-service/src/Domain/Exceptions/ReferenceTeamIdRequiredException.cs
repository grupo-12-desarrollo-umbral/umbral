namespace umbral_backend.Domain.Exceptions;

public sealed class ReferenceTeamIdRequiredException : Exception
{
    public ReferenceTeamIdRequiredException()
        : base("Reference team id is required.")
    {
    }
}
