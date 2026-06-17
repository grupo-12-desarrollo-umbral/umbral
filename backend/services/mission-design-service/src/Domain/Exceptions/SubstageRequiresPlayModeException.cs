namespace umbral_backend.Domain.Exceptions;

public sealed class SubstageRequiresPlayModeException : Exception
{
    public SubstageRequiresPlayModeException()
        : base("A substage must declare exactly one play mode.")
    {
    }
}
