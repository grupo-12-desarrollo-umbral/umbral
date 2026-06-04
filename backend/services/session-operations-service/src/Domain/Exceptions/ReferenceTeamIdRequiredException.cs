namespace umbral_backend.Domain.Exceptions;

public sealed class ReferenceTeamIdRequiredException : Exception
{
    public ReferenceTeamIdRequiredException()
        : base("A team association requires a reference team identifier.")
    {
    }
}
