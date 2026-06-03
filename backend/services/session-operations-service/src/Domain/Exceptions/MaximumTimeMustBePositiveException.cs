namespace umbral_backend.Domain.Exceptions;

public sealed class MaximumTimeMustBePositiveException : Exception
{
    public MaximumTimeMustBePositiveException()
        : base("Maximum time must be positive.")
    {
    }
}
