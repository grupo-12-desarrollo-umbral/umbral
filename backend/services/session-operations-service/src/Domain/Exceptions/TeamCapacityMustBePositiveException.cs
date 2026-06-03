namespace umbral_backend.Domain.Exceptions;

public sealed class TeamCapacityMustBePositiveException : Exception
{
    public TeamCapacityMustBePositiveException()
        : base("Team capacity must be greater than zero.")
    {
    }
}
