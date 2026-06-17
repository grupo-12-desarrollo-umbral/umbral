namespace umbral_backend.Domain.Exceptions;

public sealed class TargetSequenceOrderMustBePositiveException : Exception
{
    public TargetSequenceOrderMustBePositiveException()
        : base("Target sequence order must be a positive number.")
    {
    }
}
