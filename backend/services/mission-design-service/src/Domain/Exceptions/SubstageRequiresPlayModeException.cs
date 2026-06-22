namespace umbral_backend.Domain.Exceptions;

public sealed class SubstageRequiresPlayModeException : DomainException
{
    public SubstageRequiresPlayModeException()
        : base("A substage must declare exactly one play mode.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
