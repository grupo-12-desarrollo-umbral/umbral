namespace umbral_backend.Domain.Exceptions;

public sealed class TargetNameRequiredException : Exception
{
    public TargetNameRequiredException()
        : base("Target name is required.")
    {
    }
}
