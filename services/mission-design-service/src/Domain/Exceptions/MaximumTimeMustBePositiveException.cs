namespace umbral_backend.Domain.Exceptions;

public sealed class MaximumTimeMustBePositiveException : Exception
{
    public MaximumTimeMustBePositiveException()
        : base("Mission maximum time must be positive.")
    {
    }
}
