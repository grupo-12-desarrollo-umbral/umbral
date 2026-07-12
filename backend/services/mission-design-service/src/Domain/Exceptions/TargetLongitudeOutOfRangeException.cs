namespace umbral_backend.Domain.Exceptions;

public sealed class TargetLongitudeOutOfRangeException : DomainException
{
    public TargetLongitudeOutOfRangeException()
        : base("Target longitude must be between -180 and 180 degrees.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;

    public override string? PublicDetail => "Target longitude must be between -180 and 180 degrees.";
}
