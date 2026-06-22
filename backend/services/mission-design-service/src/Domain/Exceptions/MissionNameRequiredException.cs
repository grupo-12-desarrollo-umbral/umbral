namespace umbral_backend.Domain.Exceptions;

public sealed class MissionNameRequiredException : DomainException
{
    public MissionNameRequiredException()
        : base("Mission name is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
