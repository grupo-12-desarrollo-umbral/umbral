namespace umbral_backend.Domain.Exceptions;

public sealed class ScoreValueMustBePositiveException : Exception
{
    public ScoreValueMustBePositiveException()
        : base("Score value must be a positive number of points.")
    {
    }
}
