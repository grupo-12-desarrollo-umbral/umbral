namespace umbral_backend.Domain.Exceptions;

public sealed class MissionNodeSequenceOrderMustBePositiveException : DomainException
{
    public MissionNodeSequenceOrderMustBePositiveException()
        : base("Mission node sequence order must be a positive number.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
