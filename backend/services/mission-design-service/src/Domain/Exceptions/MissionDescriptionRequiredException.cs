namespace umbral_backend.Domain.Exceptions;

public sealed class MissionDescriptionRequiredException : DomainException
{
    public MissionDescriptionRequiredException()
        : base("Mission description is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
