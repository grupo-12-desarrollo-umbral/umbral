namespace umbral_backend.Domain.Exceptions;

public sealed class TargetLatitudeOutOfRangeException : DomainException
{
    public TargetLatitudeOutOfRangeException()
        : base("Target latitude must be between -90 and 90 degrees.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;

    public override string? PublicDetail => "Target latitude must be between -90 and 90 degrees.";
}
