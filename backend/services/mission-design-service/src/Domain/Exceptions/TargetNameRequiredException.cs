namespace umbral_backend.Domain.Exceptions;

public sealed class TargetNameRequiredException : DomainException
{
    public TargetNameRequiredException()
        : base("Target name is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
