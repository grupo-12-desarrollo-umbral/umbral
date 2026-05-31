namespace umbral_backend.Domain.Exceptions;

public sealed class DifficultyValueRequiredException : Exception
{
    public DifficultyValueRequiredException()
        : base("Mission difficulty is required.")
    {
    }
}
